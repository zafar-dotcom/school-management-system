using System.Security.Claims;
using SMS.Application.Access;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Tenancy;

namespace SMS.Api.Tenancy;

/// <summary>
/// Turns a bearer token into a validated tenant context, once per request.
/// <para>
/// The rules themselves are <see cref="ResolveTenantHandler"/>, shared with the web host —
/// a revoked membership must be refused identically whether the caller arrived with a
/// cookie or a token. What differs is only the shape of a refusal: 401 with a reason here,
/// a redirect to the login page there.
/// </para>
/// </summary>
public class ApiTenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiTenantResolutionMiddleware> _log;

    public ApiTenantResolutionMiddleware(RequestDelegate next, ILogger<ApiTenantResolutionMiddleware> log)
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
            _log.LogWarning("Tenant resolution refused: {Reason}.", reason);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "context_invalid",
                // Safe to return: it describes the caller's own access, nothing about
                // anyone else's data, and a client needs it to know to re-authenticate.
                detail = reason
            });
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
