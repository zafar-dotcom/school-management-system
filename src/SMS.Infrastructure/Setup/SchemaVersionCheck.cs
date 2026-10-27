using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public SchemaVersionCheck(
        MasterDbContext master,
        DbContextOptions<SchoolDbContext> schoolOptions,
        ILogger<SchemaVersionCheck> log)
    {
        _master = master;
        _schoolOptions = schoolOptions;
        _log = log;
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
                .AsNoTracking()
                .OrderBy(s => s.CreatedAt)
                .Select(s => new { s.Name, s.SchemaVersion })
                .FirstOrDefaultAsync(ct);

            if (record is null)
            {
                _log.LogInformation("Schemas are current but no school is registered. Setup is required.");
                return (InstallationStatus.NotInstalled, null);
            }

            var expected = ExpectedSchoolSchemaVersion(school);
            if (!string.Equals(record.SchemaVersion, expected, StringComparison.Ordinal))
            {
                // The history table says current but the school's recorded version disagrees.
                // Trust neither: something applied a migration without updating the record,
                // or the record was edited.
                var detail =
                    $"School '{record.Name}' records schema version '{record.SchemaVersion ?? "(none)"}' " +
                    $"but this build expects '{expected}'. The migrations history and the school " +
                    "record disagree; resolve before serving.";
                _log.LogError("Schema version mismatch at startup. {Detail}", detail);
                return (InstallationStatus.SchemaMismatch, detail);
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
