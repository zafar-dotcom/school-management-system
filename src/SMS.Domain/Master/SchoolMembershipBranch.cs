namespace SMS.Domain.Master;

/// <summary>
/// Which branches a Specific-scope membership covers.
/// <para>
/// SchoolId is carried here deliberately. Both foreign keys are composite — on
/// (MembershipId, SchoolId) and (BranchId, SchoolId) — so the database makes it
/// impossible to scope a membership at one school to a branch of another. Simple
/// single-column keys would allow exactly that.
/// </para>
/// </summary>
public class SchoolMembershipBranch
{
    public Guid MembershipId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid BranchId { get; set; }

    public SchoolMembership Membership { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}
