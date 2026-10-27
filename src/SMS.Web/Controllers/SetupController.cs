using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Application.Abstractions;
using SMS.Domain.Common;
using SMS.Domain.Master;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Setup;
using SMS.Web.Models;

namespace SMS.Web.Controllers;

/// <summary>
/// First run. Registers the one school, its Main branch, and the first School Admin.
/// <para>
/// Reachable only while <see cref="InstallationStatus.NotInstalled"/>; the gate middleware
/// redirects away once a school exists, so this cannot create a second one.
/// </para>
/// <para>
/// The licence row described in Master_Schema_Design section 7 is <b>not</b> written here.
/// The licence file format is still open and the signing tool does not exist yet, so there
/// is nothing to validate against. Entitlement is unenforced until both are built.
/// </para>
/// </summary>
public class SetupController : Controller
{
    private readonly MasterDbContext _master;
    private readonly UserManager<AppUser> _users;
    private readonly CrossSchemaTransaction _transaction;
    private readonly DbContextOptions<SchoolDbContext> _schoolOptions;
    private readonly InstallationState _state;
    private readonly IClock _clock;
    private readonly ILogger<SetupController> _log;

    public SetupController(
        MasterDbContext master,
        UserManager<AppUser> users,
        CrossSchemaTransaction transaction,
        DbContextOptions<SchoolDbContext> schoolOptions,
        InstallationState state,
        IClock clock,
        ILogger<SetupController> log)
    {
        _master = master;
        _users = users;
        _transaction = transaction;
        _schoolOptions = schoolOptions;
        _state = state;
        _clock = clock;
        _log = log;
    }

    [HttpGet]
    public IActionResult Index() => View(new SetupViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SetupViewModel model, CancellationToken ct)
    {
        if (!IsKnownTimeZone(model.TimeZone))
            ModelState.AddModelError(nameof(model.TimeZone), "That time zone is not recognised on this machine.");

        if (!ModelState.IsValid)
            return View(model);

        // Belt and braces: the gate middleware already blocks this, but a second school
        // would be unrecoverable without a DBA.
        if (await _master.Schools.AnyAsync(ct))
        {
            _state.Set(InstallationStatus.Ready);
            return RedirectToAction("Index", "Home");
        }

        var now = _clock.UtcNow;
        var schoolId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();

        AppUser? admin = null;

        try
        {
            await _transaction.ExecuteAsync(async token =>
            {
                _master.Schools.Add(new School
                {
                    Id = schoolId,
                    Name = model.SchoolName.Trim(),
                    // Version 1 serves the one schema that the installer created. The slug
                    // matches it so the two can never drift apart; the school's own name is
                    // free text and carries no constraint.
                    Slug = "main",
                    SchemaName = SchoolDbContext.DefaultSchema,
                    Status = SchoolStatus.Active,
                    SchemaVersion = SchemaVersionCheck.ExpectedSchoolSchemaVersion(_schoolOptions),
                    LastMigratedAt = now
                });

                _master.Branches.Add(new Branch
                {
                    Id = branchId,
                    SchoolId = schoolId,
                    Code = "MAIN",
                    Name = model.BranchName.Trim(),
                    Status = BranchStatus.Active,
                    IsDefault = true,
                    TimeZone = model.TimeZone.Trim()
                });

                await _master.SaveChangesAsync(token);

                admin = new AppUser
                {
                    Id = Guid.CreateVersion7(),
                    UserName = model.AdminEmail.Trim(),
                    Email = model.AdminEmail.Trim(),
                    EmailConfirmed = true,
                    DisplayName = model.AdminDisplayName.Trim(),
                    Status = UserStatus.Active,
                    IsPlatformAdmin = false,
                    CreatedAt = now
                };

                var created = await _users.CreateAsync(admin, model.Password);
                if (!created.Succeeded)
                    throw new SetupFailedException(created.Errors.Select(e => e.Description).ToArray());

                _master.Memberships.Add(new SchoolMembership
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
        catch (SetupFailedException ex)
        {
            foreach (var error in ex.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        _state.Set(InstallationStatus.Ready);
        _log.LogInformation(
            "Setup complete. School {SchoolId}, branch {BranchId}, first administrator {UserId}.",
            schoolId, branchId, admin!.Id);

        TempData["SetupMessage"] = "Setup is complete. Sign in with the administrator account you just created.";
        return RedirectToAction("Login", "Account");
    }

    /// <summary>
    /// Branch time zones are IANA identifiers. .NET on Windows accepts them, but only ones
    /// the machine actually knows — storing an unknown value would break every date display
    /// on that branch.
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

    private sealed class SetupFailedException(string[] errors) : Exception("Setup failed.")
    {
        public string[] Errors { get; } = errors;
    }
}
