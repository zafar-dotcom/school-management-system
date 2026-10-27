using SMS.Domain.Common;

namespace SMS.Domain.Master;

/// <summary>
/// The authorization record: who holds which role at which school, over which branches.
/// <para>
/// This is the only source of truth for authorization. ASP.NET Identity's role tables
/// are deliberately not created — a (user, role) pair has no tenant dimension and cannot
/// express per-school or per-branch access. Never call UserManager.GetRolesAsync.
/// </para>
/// </summary>
public class SchoolMembership : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }

    public SchoolRole Role { get; set; }
    public BranchScope BranchScope { get; set; }
    public MembershipStatus Status { get; set; }

    /// <summary>
    /// Incremented whenever role, scope or status changes. Carried as a session claim and
    /// re-checked per request, so revoking access takes effect without waiting for the
    /// cookie to expire.
    /// </summary>
    public int MembershipVersion { get; set; } = 1;

    public AppUser User { get; set; } = null!;
    public School School { get; set; } = null!;

    /// <summary>Populated only when <see cref="BranchScope"/> is Specific.</summary>
    public ICollection<SchoolMembershipBranch> Branches { get; set; } = new List<SchoolMembershipBranch>();
}
