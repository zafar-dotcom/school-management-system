using SMS.Application.Abstractions;
using SMS.Domain.Master;

namespace SMS.Application.Access;

/// <summary>
/// Turns a user id into the school context they sign in with.
/// <para>
/// Used at sign-in and again whenever Identity refreshes a principal, so it must stay
/// cheap and side-effect free.
/// </para>
/// </summary>
public sealed class BuildClaimSetHandler
{
    private readonly IMasterStore _master;

    public BuildClaimSetHandler(IMasterStore master) => _master = master;

    /// <summary>Null when the user holds no active membership, or no branch is reachable.</summary>
    public async Task<SchoolClaimSet?> HandleAsync(Guid userId, CancellationToken ct = default)
    {
        var membership = await _master.FindActiveMembershipAsync(userId, ct);
        if (membership is null)
            return null;

        var branchId = await _master.FindLandingBranchIdAsync(membership, ct);
        if (branchId is null)
            return null;

        return ToClaimSet(membership, branchId.Value);
    }

    internal static SchoolClaimSet ToClaimSet(SchoolMembership membership, Guid branchId) =>
        new(membership.SchoolId, branchId, membership.BranchScope, membership.Role, membership.MembershipVersion);
}
