using Microsoft.AspNetCore.Identity;
using SMS.Application.Abstractions;
using SMS.Application.Common;
using SMS.Domain.Common;
using SMS.Domain.Master;

namespace SMS.Application.Access;

public sealed record AuthenticateCommand(string Email, string Password);

/// <summary>
/// The authenticated user and the school context they carry. What each host does next
/// differs — the web host writes a cookie, the API issues a token — but everything up to
/// this point is identical.
/// </summary>
public sealed record AuthenticatedUser(AppUser User, SchoolClaimSet ClaimSet);

/// <summary>
/// Validates credentials, applies lockout, and resolves the caller's school context.
/// <para>
/// This deliberately does <b>not</b> sign anyone in. How an identity is persisted is
/// host-specific; validating it is not. That is the seam architecture section 11 describes,
/// and it is why this uses <see cref="UserManager{TUser}"/> rather than
/// <c>SignInManager</c> — the latter is tied to ASP.NET Core's cookie pipeline and would
/// drag the web host's concerns into a layer the API also uses.
/// </para>
/// </summary>
public sealed class AuthenticateHandler
{
    /// <summary>
    /// One message for every credential failure. Distinguishing "no such account" from
    /// "wrong password" tells an attacker which email addresses are real.
    /// </summary>
    private const string CredentialsRejected = "That email address and password do not match an account.";

    private const string NotActive = "This account is not active. Contact your school administrator.";
    private const string LockedOut = "This account is locked after too many attempts. Try again later.";
    private const string NoAccess = "This account has no active access to a school. Contact your school administrator.";

    private readonly UserManager<AppUser> _users;
    private readonly BuildClaimSetHandler _claims;

    public AuthenticateHandler(UserManager<AppUser> users, BuildClaimSetHandler claims)
    {
        _users = users;
        _claims = claims;
    }

    public async Task<Result<AuthenticatedUser>> HandleAsync(
        AuthenticateCommand command, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(command.Email.Trim());
        if (user is null)
            return Result<AuthenticatedUser>.Failure(CredentialsRejected);

        if (await _users.IsLockedOutAsync(user))
            return Result<AuthenticatedUser>.Failure(LockedOut);

        // Status is an administrative decision and is checked before the password, so a
        // disabled account cannot be probed for a valid password.
        if (user.Status != UserStatus.Active)
            return Result<AuthenticatedUser>.Failure(NotActive);

        if (!await _users.CheckPasswordAsync(user, command.Password))
        {
            // Counting failures is what makes the lockout above mean anything.
            await _users.AccessFailedAsync(user);

            return await _users.IsLockedOutAsync(user)
                ? Result<AuthenticatedUser>.Failure(LockedOut)
                : Result<AuthenticatedUser>.Failure(CredentialsRejected);
        }

        await _users.ResetAccessFailedCountAsync(user);

        // Correct password but no way into a school: refuse here with a clear message
        // rather than letting tenant resolution bounce them around later.
        var claimSet = await _claims.HandleAsync(user.Id, ct);
        if (claimSet is null)
            return Result<AuthenticatedUser>.Failure(NoAccess);

        return Result.Success(new AuthenticatedUser(user, claimSet));
    }
}
