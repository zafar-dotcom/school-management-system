using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SMS.Application.Access;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Tenancy;

namespace SMS.Web.Tenancy;

/// <summary>
/// Turns an authenticated cookie into a validated tenant context, once per request.
/// <para>
/// The validation rules live in <see cref="ResolveTenantHandler"/>, shared with the API —
/// otherwise the cookie path and the token path would each carry a copy, free to drift.
/// What remains here is reading claims off an <see cref="HttpContext"/> and deciding what
/// a refusal looks like in a browser: sign out and send them back to the login page.
/// </para>
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _log;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(
        HttpContext context, ResolveTenantHandler resolver, TenantContext tenant)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var reason = await ResolveAsync(context, resolver, tenant);
        if (reason is not null)
        {
            _log.LogWarning("Tenant resolution refused: {Reason}. Signing the user out.", reason);
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Response.Redirect("/Account/Login?reason=context");
            return;
        }

        await _next(context);
    }

    private static async Task<string?> ResolveAsync(
        HttpContext context, ResolveTenantHandler resolver, TenantContext tenant)
    {
        if (!TryGuid(context.User, SchoolClaimTypes.SchoolId, out var schoolId))
            return "no school claim";
        if (!TryGuid(context.User, SchoolClaimTypes.ActiveBranchId, out var branchId))
            return "no active branch claim";
        if (!TryGuid(context.User, ClaimTypes.NameIdentifier, out var userId))
            return "no user id claim";
        if (!int.TryParse(context.User.FindFirstValue(SchoolClaimTypes.MembershipVersion), out var version))
            return "no membership version claim";

        var result = await resolver.HandleAsync(
            new TenantClaims(userId, schoolId, branchId, version),
            SchoolDbContext.DefaultSchema,
            context.RequestAborted);

        if (!result.Succeeded)
            return string.Join("; ", result.Errors);

        var resolved = result.Value;
        tenant.Resolve(resolved.SchoolId, resolved.SchemaName, resolved.ActiveBranchId, resolved.UserId);
        return null;
    }

    private static bool TryGuid(ClaimsPrincipal user, string claimType, out Guid value) =>
        Guid.TryParse(user.FindFirstValue(claimType), out value) && value != Guid.Empty;
}
