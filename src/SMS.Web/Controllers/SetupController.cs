using Microsoft.AspNetCore.Mvc;
using SMS.Application.Setup;
using SMS.Infrastructure.Setup;
using SMS.Web.Models;

namespace SMS.Web.Controllers;

/// <summary>
/// First run. Reachable only while <see cref="InstallationStatus.NotInstalled"/> — the gate
/// middleware redirects away once a school exists, so this cannot create a second one.
/// <para>
/// Everything this used to do now lives in <see cref="CompleteSetupHandler"/>, so the API
/// can offer the same operation without copying it.
/// </para>
/// </summary>
public class SetupController : Controller
{
    private readonly CompleteSetupHandler _completeSetup;
    private readonly InstallationState _state;
    private readonly ILogger<SetupController> _log;

    public SetupController(
        CompleteSetupHandler completeSetup,
        InstallationState state,
        ILogger<SetupController> log)
    {
        _completeSetup = completeSetup;
        _state = state;
        _log = log;
    }

    [HttpGet]
    public IActionResult Index() => View(new SetupViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SetupViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _completeSetup.HandleAsync(
            new CompleteSetupCommand(
                model.SchoolName,
                model.BranchName,
                model.TimeZone,
                model.AdminDisplayName,
                model.AdminEmail,
                model.Password),
            ct);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);

            return View(model);
        }

        _state.Set(InstallationStatus.Ready);

        _log.LogInformation(
            "Setup complete. School {SchoolId}, branch {BranchId}, first administrator {UserId}.",
            result.Value.SchoolId, result.Value.BranchId, result.Value.AdministratorId);

        TempData["SetupMessage"] =
            "Setup is complete. Sign in with the administrator account you just created.";

        return RedirectToAction("Login", "Account");
    }
}
