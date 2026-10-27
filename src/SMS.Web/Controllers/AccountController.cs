using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SMS.Application.Access;
using SMS.Domain.Master;
using SMS.Web.Models;

namespace SMS.Web.Controllers;

/// <summary>
/// Cookie sign-in for the browser.
/// <para>
/// Credential validation, lockout and membership resolution live in
/// <see cref="AuthenticateHandler"/>, shared with the API. What stays here is the one
/// genuinely host-specific part: writing the identity into a cookie. The API writes a token
/// from the same handler's result — architecture section 11.
/// </para>
/// </summary>
public class AccountController : Controller
{
    private readonly AuthenticateHandler _authenticate;
    private readonly SignInManager<AppUser> _signIn;
    private readonly ILogger<AccountController> _log;

    public AccountController(
        AuthenticateHandler authenticate,
        SignInManager<AppUser> signIn,
        ILogger<AccountController> log)
    {
        _authenticate = authenticate;
        _signIn = signIn;
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
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _authenticate.HandleAsync(
            new AuthenticateCommand(model.Email, model.Password), ct);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);

            return View(model);
        }

        // The claim set is rebuilt by the claims factory during sign-in, so it is not
        // passed across — see SchoolClaimsPrincipalFactory.
        await _signIn.SignInAsync(result.Value.User, model.RememberMe);

        _log.LogInformation("User {UserId} signed in.", result.Value.User.Id);

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
