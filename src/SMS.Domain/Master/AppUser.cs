using Microsoft.AspNetCore.Identity;
using SMS.Domain.Common;

namespace SMS.Domain.Master;

/// <summary>
/// A login account. Identity owns credentials and lockout; this type adds the
/// account-level fields the product needs.
/// <para>
/// This holds the account label only. The name a school uses for a person lives on
/// their staff or student record in the school schema — see architecture section 4.7.
/// </para>
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = null!;

    /// <summary>Administrative state, distinct from Identity's temporary lockout.</summary>
    public UserStatus Status { get; set; }

    /// <summary>Platform administrator. Always false in version 1 — there is no platform to administer.</summary>
    public bool IsPlatformAdmin { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public ICollection<SchoolMembership> Memberships { get; set; } = new List<SchoolMembership>();
}
