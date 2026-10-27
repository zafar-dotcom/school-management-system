using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMS.Application.Abstractions;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Tenancy;

namespace SMS.Infrastructure.Setup;

/// <summary>
/// Decides, once at startup, whether this build may serve the database it is pointed at.
/// <para>
/// It never migrates anything. Migrations are a deliberate installer step — architecture
/// section 11. This only looks, and reports what it found.
/// </para>
/// </summary>
public sealed class SchemaVersionCheck
{
    private readonly MasterDbContext _master;
    private readonly DbContextOptions<SchoolDbContext> _schoolOptions;
    private readonly ILogger<SchemaVersionCheck> _log;
    private readonly IClock _clock;

    public SchemaVersionCheck(
        MasterDbContext master,
        DbContextOptions<SchoolDbContext> schoolOptions,
        ILogger<SchemaVersionCheck> log,
        IClock clock)
    {
        _master = master;
        _schoolOptions = schoolOptions;
        _log = log;
        _clock = clock;
    }

    /// <summary>The newest migration this build carries for the school schema.</summary>
    public static string ExpectedSchoolSchemaVersion(DbContext schoolContext) =>
        schoolContext.Database.GetMigrations().Last();

    /// <summary>
    /// The same value, for callers that have the options but no context — the setup wizard,
    /// which must stamp <c>school.schema_version</c> before any tenant exists.
    /// </summary>
    public static string ExpectedSchoolSchemaVersion(DbContextOptions<SchoolDbContext> options)
    {
        using var context = new SchoolDbContext(options, new MetadataOnlyTenantContext());
        return ExpectedSchoolSchemaVersion(context);
    }

    public async Task<(InstallationStatus Status, string? Detail)> RunAsync(CancellationToken ct = default)
    {
        // A metadata-only tenant is correct here: this reads the migrations history and the
        // model, never a branch-scoped row.
        await using var school = new SchoolDbContext(_schoolOptions, new MetadataOnlyTenantContext());

        try
        {
            var masterPending = (await _master.Database.GetPendingMigrationsAsync(ct)).ToList();
            var schoolPending = (await school.Database.GetPendingMigrationsAsync(ct)).ToList();

            if (masterPending.Count > 0 || schoolPending.Count > 0)
            {
                var detail =
                    $"The database is behind this build. Unapplied migrations — " +
                    $"master: {Describe(masterPending)}; school: {Describe(schoolPending)}. " +
                    "Run the installer or 'dotnet ef database update' for both contexts.";
                _log.LogError("Schema mismatch at startup. {Detail}", detail);
                return (InstallationStatus.SchemaMismatch, detail);
            }

            // Schemas are current. Is the product actually set up?
            // Ordered so the choice is deterministic rather than whatever the scan returns
            // first. Version 1 has one school; this is what makes that assumption explicit.
            var record = await _master.Schools
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (record is null)
            {
                _log.LogInformation("Schemas are current but no school is registered. Setup is required.");
                return (InstallationStatus.NotInstalled, null);
            }

            var expected = ExpectedSchoolSchemaVersion(school);
            if (!string.Equals(record.SchemaVersion, expected, StringComparison.Ordinal))
            {
                // Reaching here means the migrations history is fully applied — that check
                // ran above and passed — but school.schema_version still names an older
                // migration. The history is authoritative; this column is the fast-lookup
                // cache that architecture section 4.8 keeps beside it. So the schema is
                // genuinely correct and the cache is simply stale.
                //
                // Refusing to serve would be wrong, and worse than wrong operationally:
                // only the setup wizard ever writes this column, so the FIRST update
                // shipped to a school would brick the install until someone edited the row
                // by hand, on a machine with no DBA. Refresh it and carry on.
                _log.LogWarning(
                    "School '{School}' recorded schema version {Recorded} but the database is " +
                    "migrated to {Expected}. The migrations history is authoritative, so the " +
                    "record is being brought up to date.",
                    record.Name, record.SchemaVersion ?? "(none)", expected);

                record.SchemaVersion = expected;
                record.LastMigratedAt = _clock.UtcNow;
                await _master.SaveChangesAsync(ct);
            }

            _log.LogInformation(
                "Schema check passed. School '{School}' at version {Version}.", record.Name, expected);
            return (InstallationStatus.Ready, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "The startup schema check could not reach the database.");
            return (InstallationStatus.Unavailable, ex.Message);
        }
    }

    private static string Describe(IReadOnlyCollection<string> pending) =>
        pending.Count == 0 ? "none" : string.Join(", ", pending);
}
