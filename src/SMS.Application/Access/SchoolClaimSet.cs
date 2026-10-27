using System.Security.Claims;
using SMS.Domain.Common;

namespace SMS.Application.Access;

/// <summary>
/// The school context a signed-in user carries. Built once here, then written into a cookie
/// by the web host or a token by the API host.
/// </summary>
public sealed record SchoolClaimSet(
    Guid SchoolId,
    Guid ActiveBranchId,
    BranchScope BranchScope,
    SchoolRole Role,
    int MembershipVersion)
{
    public IEnumerable<Claim> ToClaims()
    {
        yield return new Claim(SchoolClaimTypes.SchoolId, SchoolId.ToString());
        yield return new Claim(SchoolClaimTypes.ActiveBranchId, ActiveBranchId.ToString());
        yield return new Claim(SchoolClaimTypes.BranchScope, BranchScope.ToString());
        yield return new Claim(SchoolClaimTypes.SchoolRole, Role.ToString());
        yield return new Claim(SchoolClaimTypes.MembershipVersion, MembershipVersion.ToString());
    }
}
