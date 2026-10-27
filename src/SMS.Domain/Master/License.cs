using SMS.Domain.Common;

namespace SMS.Domain.Master;

/// <summary>
/// A yearly subscription. Renewal inserts a new row and supersedes the previous one,
/// so the renewal history is preserved.
/// <para>
/// <b>Only <see cref="SignedPayload"/> is authoritative.</b> Every other field is a decoded
/// cache for display and limit checks. Validation always verifies the signature and reads
/// the dates and limits from inside the payload — a customer with database access can edit
/// the columns and change nothing.
/// </para>
/// </summary>
public class License : AuditableEntity
{
    public Guid SchoolId { get; set; }

    /// <summary>Must match the installation's key, or the licence belongs to a different install.</summary>
    public Guid InstallationKey { get; set; }

    public string LicenseNumber { get; set; } = null!;
    public string LicensedTo { get; set; } = null!;

    /// <summary>The signed licence file, verbatim. The single source of truth.</summary>
    public string SignedPayload { get; set; } = null!;

    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>End of the post-expiry grace period. After this the system becomes read-only.</summary>
    public DateTimeOffset GraceUntil { get; set; }

    public int? MaxBranches { get; set; }
    public int? MaxStudents { get; set; }

    public LicenseStatus Status { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }

    /// <summary>Set when a renewal replaces this licence. Null means this is the active one.</summary>
    public DateTimeOffset? SupersededAt { get; set; }

    public School School { get; set; } = null!;
}
