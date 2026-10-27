using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SMS.Application.Access;
using SMS.Domain.Master;

namespace SMS.Web.Tenancy;

/// <summary>
/// Adds the school and branch claims to the principal at sign-in, and again whenever
/// Identity refreshes it after a security-stamp change.
/// <para>
/// The claim set itself is built by <see cref="BuildClaimSetHandler"/> in the application
/// layer, so the API's token issuer emits exactly the same claims. This class only decides
/// where they are written.
/// </para>
/// </summary>
public class SchoolClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser>
{
    private readonly BuildClaimSetHandler _claims;

    public SchoolClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        IOptions<IdentityOptions> options,
        BuildClaimSetHandler claims)
        : base(userManager, options)
    {
        _claims = claims;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Null when the user holds no active membership. The sign-in path refuses that
        // case already; leaving the claims off means tenant resolution refuses it too.
        var claimSet = await _claims.HandleAsync(user.Id);
        if (claimSet is not null)
            identity.AddClaims(claimSet.ToClaims());

        return identity;
    }
}
