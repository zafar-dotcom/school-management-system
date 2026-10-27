using Microsoft.AspNetCore.Identity;
using SMS.Application.Abstractions;
using SMS.Application.Common;
using SMS.Domain.Common;
using SMS.Domain.Master;

namespace SMS.Application.Setup;

public sealed record CompleteSetupCommand(
    string SchoolName,
    string BranchName,
    string TimeZone,
    string AdminDisplayName,
    string AdminEmail,
    string Password);

public sealed record SetupOutcome(Guid SchoolId, Guid BranchId, Guid AdministratorId);

/// <summary>
/// First run: registers the one school, its Main branch, and the first School Admin, all in
/// a single transaction.
/// <para>
/// No licence row is written. The licence file format and the signing tool do not exist
/// yet, so there would be nothing to validate the row against — see PROJECT_STATUS section 5.
/// </para>
/// </summary>
public sealed class CompleteSetupHandler
{
    /// <summary>
    /// Version 1 serves the one schema the installer created. The slug is fixed to match it
    /// so the two can never drift; the school's own name is free text and unconstrained.
    /// </summary>
    private const string VersionOneSlug = "main";

    private const string VersionOneSchema = "school_main";

    private readonly IMasterStore _master;
    private readonly UserManager<AppUser> _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchemaVersionSource _schemaVersion;
    private readonly IClock _clock;

    public CompleteSetupHandler(
        IMasterStore master,
        UserManager<AppUser> users,
        IUnitOfWork unitOfWork,
        ISchemaVersionSource schemaVersion,
        IClock clock)
    {
        _master = master;
        _users = users;
        _unitOfWork = unitOfWork;
        _schemaVersion = schemaVersion;
        _clock = clock;
    }

    public async Task<Result<SetupOutcome>> HandleAsync(
        CompleteSetupCommand command, CancellationToken ct = default)
    {
        if (!IsKnownTimeZone(command.TimeZone))
            return Result<SetupOutcome>.Failure("That time zone is not recognised on this machine.");

        // The caller gates this too, but a second school would be unrecoverable without a
        // DBA on site, so it is checked here as well.
        if (await _master.AnySchoolExistsAsync(ct))
            return Result<SetupOutcome>.Failure("This installation is already set up.");

        var now = _clock.UtcNow;
        var schoolId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();
        var adminId = Guid.CreateVersion7();

        string[]? identityErrors = null;

        try
        {
            await _unitOfWork.ExecuteAsync(async token =>
            {
                _master.Add(new School
                {
                    Id = schoolId,
                    Name = command.SchoolName.Trim(),
                    Slug = VersionOneSlug,
                    SchemaName = VersionOneSchema,
                    Status = SchoolStatus.Active,
                    SchemaVersion = _schemaVersion.ExpectedSchoolSchemaVersion,
                    LastMigratedAt = now
                });

                _master.Add(new Branch
                {
                    Id = branchId,
                    SchoolId = schoolId,
                    Code = "MAIN",
                    Name = command.BranchName.Trim(),
                    Status = BranchStatus.Active,
                    IsDefault = true,
                    TimeZone = command.TimeZone.Trim()
                });

                await _master.SaveChangesAsync(token);

                var admin = new AppUser
                {
                    Id = adminId,
                    UserName = command.AdminEmail.Trim(),
                    Email = command.AdminEmail.Trim(),
                    EmailConfirmed = true,
                    DisplayName = command.AdminDisplayName.Trim(),
                    Status = UserStatus.Active,
                    IsPlatformAdmin = false,
                    CreatedAt = now
                };

                var created = await _users.CreateAsync(admin, command.Password);
                if (!created.Succeeded)
                {
                    identityErrors = created.Errors.Select(e => e.Description).ToArray();

                    // Throwing is what rolls the school and branch back. A weak password
                    // must not leave a half-created installation behind.
                    throw new SetupRejectedException();
                }

                _master.Add(new SchoolMembership
                {
                    Id = Guid.CreateVersion7(),
                    UserId = admin.Id,
                    SchoolId = schoolId,
                    Role = SchoolRole.SchoolAdmin,
                    BranchScope = BranchScope.AllBranches,
                    Status = MembershipStatus.Active,
                    MembershipVersion = 1
                });

                await _master.SaveChangesAsync(token);
            }, ct);
        }
        catch (SetupRejectedException)
        {
            // Expected: the transaction rolled back and the errors are reported below.
            // Any other exception is a real fault and keeps propagating.
        }

        return identityErrors is not null
            ? Result<SetupOutcome>.Failure(identityErrors)
            : Result.Success(new SetupOutcome(schoolId, branchId, adminId));
    }

    /// <summary>
    /// Branch time zones are IANA identifiers. .NET on Windows accepts them, but only ones
    /// the machine knows — an unrecognised value would break every date shown for that
    /// branch, and the column is NOT NULL precisely so that cannot happen quietly.
    /// </summary>
    private static bool IsKnownTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private sealed class SetupRejectedException : Exception;
}
