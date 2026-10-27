using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SMS.Domain.Common;
using SMS.Domain.Master;
using SMS.Infrastructure.Persistence;
using SMS.Web.Models;
using SMS.Web.Tenancy;

namespace SMS.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly UserManager<AppUser> _users;
    private readonly MasterDbContext _master;
    private readonly ILogger<AccountController> _log;

    public AccountController(
        SignInManager<AppUser> signIn,
        UserManager<AppUser> users,
        MasterDbContext master,
        ILogger<AccountController> log)
    {
        _signIn = signIn;
        _users = users;
        _master = master;
        _log = log;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? reason = null)
    {
        if (reason == "context")
            ModelState.AddModelError(string.Empty,
                "Your access has changed since you signed in. Please sign in again.");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _users.FindByEmailAsync(model.Email.Trim());

        // One message for every failure. Saying "no such account" tells an attacker which
        // email addresses are real.
        const string failed = "That email address and password do not match an account.";

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, failed);
            return View(model);
        }

        if (user.Status != UserStatus.Active)
        {
            _log.LogWarning("Sign-in refused for {UserId}: account status is {Status}.", user.Id, user.Status);
            ModelState.AddModelError(string.Empty,
                "This account is not active. Contact your school administrator.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "This account is locked after too many attempts. Try again later.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, failed);
            return View(model);
        }

        // Authentication succeeded, but a login with no active membership has no school and
        // no branch, so it cannot reach anything. Signing out here gives a clear message
        // instead of a confusing redirect loop at tenant resolution.
        var membership = await SchoolClaimsPrincipalFactory.ActiveMembershipAsync(_master, user.Id);
        if (membership is null
            || await SchoolClaimsPrincipalFactory.ActiveBranchIdAsync(_master, membership) is null)
        {
            await _signIn.SignOutAsync();
            _log.LogWarning("Signed {UserId} back out: no active membership or no reachable branch.", user.Id);
            ModelState.AddModelError(string.Empty,
                "This account has no active access to a school. Contact your school administrator.");
            return View(model);
        }

        _log.LogInformation("User {UserId} signed in.", user.Id);

        return Redirect(SafeReturnUrl(model.ReturnUrl));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Denied() => View();

    /// <summary>Only ever redirect inside this application — an open redirect is a phishing tool.</summary>
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
