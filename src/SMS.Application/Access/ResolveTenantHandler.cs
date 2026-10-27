using SMS.Application.Abstractions;
using SMS.Application.Common;
using SMS.Domain.Common;

namespace SMS.Application.Access;

/// <summary>What a caller claims to be. Already extracted from a cookie or a token.</summary>
public sealed record TenantClaims(Guid UserId, Guid SchoolId, Guid ActiveBranchId, int MembershipVersion);

/// <summary>What the database agrees they are.</summary>
public sealed record ResolvedTenant(Guid SchoolId, string SchemaName, Guid ActiveBranchId, Guid UserId);

/// <summary>
/// Re-validates a caller's claimed school and branch against <c>master</c>, every request.
/// <para>
/// A signed cookie or token cannot be forged, but it can be <b>stale</b>: a membership
/// revoked a minute ago is still inside it. So nothing here is taken on trust, and the
/// schema name is read from the School record rather than from the claims — architecture
/// section 4.5 layer 2 requires it to come from validated data.
/// </para>
/// <para>
/// Shared by both hosts. The web host would otherwise validate a cookie and the API a
/// token, with two copies of these rules free to drift apart.
/// </para>
/// </summary>
public sealed class ResolveTenantHandler
{
    private readonly IMasterStore _master;

    public ResolveTenantHandler(IMasterStore master) => _master = master;

    public async Task<Result<ResolvedTenant>> HandleAsync(
        TenantClaims claims, string expectedSchemaName, CancellationToken ct = default)
    {
        var school = await _master.FindSchoolAsync(claims.SchoolId, ct);
        if (school is null)
            return Result<ResolvedTenant>.Failure("school not found");
        if (school.Status != SchoolStatus.Active)
            return Result<ResolvedTenant>.Failure($"school status is {school.Status}");

        // This install serves exactly one schema, fixed at build time. A school record
        // naming a different one means this database belongs to another installation.
        if (!string.Equals(school.SchemaName, expectedSchemaName, StringComparison.Ordinal))
            return Result<ResolvedTenant>.Failure(
                $"school schema '{school.SchemaName}' is not this installation's '{expectedSchemaName}'");

        var membership = await _master.FindMembershipAsync(claims.UserId, claims.SchoolId, ct);
        if (membership is null)
            return Result<ResolvedTenant>.Failure("membership not found");
        if (membership.Status != MembershipStatus.Active)
            return Result<ResolvedTenant>.Failure("membership is not active");

        // Revocation must not wait for the cookie or token to expire — architecture 5.6.
        if (membership.MembershipVersion != claims.MembershipVersion)
            return Result<ResolvedTenant>.Failure("membership has changed since sign-in");

        var branch = await _master.FindBranchAsync(claims.ActiveBranchId, ct);
        if (branch is null)
            return Result<ResolvedTenant>.Failure("branch not found");
        if (branch.SchoolId != claims.SchoolId)
            return Result<ResolvedTenant>.Failure("branch belongs to another school");
        if (branch.Status != BranchStatus.Active)
            return Result<ResolvedTenant>.Failure($"branch status is {branch.Status}");

        // The active branch must sit inside the user's scope, or a token minted before the
        // scope was narrowed would still reach it.
        if (membership.BranchScope == BranchScope.Specific
            && !await _master.IsBranchInScopeAsync(membership.Id, claims.ActiveBranchId, ct))
        {
            return Result<ResolvedTenant>.Failure("active branch is outside the user's branch scope");
        }

        return Result.Success(
            new ResolvedTenant(school.Id, school.SchemaName, branch.Id, claims.UserId));
    }
}
