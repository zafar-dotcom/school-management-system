using SMS.Domain.Master;

namespace SMS.Application.Abstractions;

/// <summary>
/// The <c>master</c> schema, as the use cases need it.
/// <para>
/// Deliberately a named set of operations rather than a generic repository — architecture
/// section 10. It exists so that <c>SMS.Application</c> can stay free of a reference to
/// <c>SMS.Infrastructure</c>, which is the one dependency rule the layering rests on.
/// </para>
/// </summary>
public interface IMasterStore
{
    Task<bool> AnySchoolExistsAsync(CancellationToken ct = default);

    void Add(School school);
    void Add(Branch branch);
    void Add(SchoolMembership membership);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Version 1 has exactly one active membership per user. Under multi-school this becomes
    /// "the membership for the school the user selected" and nothing above it changes.
    /// </summary>
    Task<SchoolMembership?> FindActiveMembershipAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The branch a user lands in: the school's default branch for all-branches scope, or
    /// the first branch they actually hold. Null when neither exists or is active.
    /// </summary>
    Task<Guid?> FindLandingBranchIdAsync(SchoolMembership membership, CancellationToken ct = default);

    Task<SchoolRecord?> FindSchoolAsync(Guid schoolId, CancellationToken ct = default);

    Task<MembershipRecord?> FindMembershipAsync(Guid userId, Guid schoolId, CancellationToken ct = default);

    Task<BranchRecord?> FindBranchAsync(Guid branchId, CancellationToken ct = default);

    Task<bool> IsBranchInScopeAsync(Guid membershipId, Guid branchId, CancellationToken ct = default);

    /// <summary>School and branch display names for the current context.</summary>
    Task<ContextNames?> FindContextNamesAsync(Guid branchId, CancellationToken ct = default);
}

public sealed record SchoolRecord(Guid Id, string Name, string SchemaName, SMS.Domain.Common.SchoolStatus Status);

public sealed record MembershipRecord(
    Guid Id,
    SMS.Domain.Common.MembershipStatus Status,
    int MembershipVersion,
    SMS.Domain.Common.BranchScope BranchScope,
    SMS.Domain.Common.SchoolRole Role);

public sealed record BranchRecord(Guid Id, Guid SchoolId, SMS.Domain.Common.BranchStatus Status);

public sealed record ContextNames(string SchoolName, string BranchName);
