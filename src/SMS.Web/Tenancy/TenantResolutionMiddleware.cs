using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SMS.Domain.Common;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Tenancy;

namespace SMS.Web.Tenancy;

/// <summary>
/// Turns an authenticated cookie into a validated tenant context, once per request.
/// <para>
/// The cookie is signed, so its claims cannot be forged — but they can be <i>stale</i>. A
/// membership revoked a minute ago is still in the cookie. So every claim here is checked
/// against <c>master</c> before it is trusted, and the schema name is taken from the School
/// record rather than from the cookie: architecture section 4.5 layer 2 requires it to be
/// derived from validated data, never from anything carried in the request.
/// </para>
/// <para>
/// Anything that fails signs the user out rather than degrading quietly. A half-resolved
/// context is the shape cross-tenant bugs take.
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

    public async Task InvokeAsync(HttpContext context, MasterDbContext master, TenantContext tenant)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var failure = await ResolveAsync(context, master, tenant);
        if (failure is not null)
        {
            _log.LogWarning("Tenant resolution refused: {Reason}. Signing the user out.", failure);
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Response.Redirect("/Account/Login?reason=context");
            return;
        }

        await _next(context);
    }

    private static async Task<string?> ResolveAsync(
        HttpContext context, MasterDbContext master, TenantContext tenant)
    {
        if (!TryGuid(context.User, SchoolClaims.SchoolId, out var schoolId))
            return "no school claim";
        if (!TryGuid(context.User, SchoolClaims.ActiveBranchId, out var branchId))
            return "no active branch claim";
        if (!TryGuid(context.User, ClaimTypes.NameIdentifier, out var userId))
            return "no user id claim";
        if (!int.TryParse(context.User.FindFirstValue(SchoolClaims.MembershipVersion), out var version))
            return "no membership version claim";

        var school = await master.Schools
            .AsNoTracking()
            .Where(s => s.Id == schoolId)
            .Select(s => new { s.Id, s.SchemaName, s.Status })
            .FirstOrDefaultAsync();

        if (school is null) return "school not found";
        if (school.Status != SchoolStatus.Active) return $"school status is {school.Status}";

        // This install serves exactly one schema, fixed at build time. A school record
        // naming a different one means the database belongs to another installation.
        if (!string.Equals(school.SchemaName, SchoolDbContext.DefaultSchema, StringComparison.Ordinal))
            return $"school schema '{school.SchemaName}' is not this installation's '{SchoolDbContext.DefaultSchema}'";

        // Revocation must not wait for the cookie to expire — architecture section 5.6.
        var membership = await master.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.SchoolId == schoolId)
            .Select(m => new { m.Status, m.MembershipVersion, m.BranchScope, m.Id })
            .FirstOrDefaultAsync();

        if (membership is null) return "membership not found";
        if (membership.Status != MembershipStatus.Active) return "membership is not active";
        if (membership.MembershipVersion != version) return "membership has changed since sign-in";

        var branch = await master.Branches
            .AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Id, b.SchoolId, b.Status })
            .FirstOrDefaultAsync();

        if (branch is null) return "branch not found";
        if (branch.SchoolId != schoolId) return "branch belongs to another school";
        if (branch.Status != BranchStatus.Active) return $"branch status is {branch.Status}";

        // The active branch must sit inside the user's scope, or a cookie edited before the
        // scope was narrowed would still reach it.
        if (membership.BranchScope == BranchScope.Specific)
        {
            var inScope = await master.MembershipBranches
                .AsNoTracking()
                .AnyAsync(mb => mb.MembershipId == membership.Id && mb.BranchId == branchId);

            if (!inScope) return "active branch is outside the user's branch scope";
        }

        tenant.Resolve(schoolId, school.SchemaName, branchId, userId);
        return null;
    }

    private static bool TryGuid(ClaimsPrincipal user, string claimType, out Guid value) =>
        Guid.TryParse(user.FindFirstValue(claimType), out value) && value != Guid.Empty;
}
