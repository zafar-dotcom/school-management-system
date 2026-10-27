using SMS.Domain.Common;

namespace SMS.Domain.Master;

/// <summary>
/// A campus. Version 1 creates exactly one, coded MAIN, flagged <see cref="IsDefault"/>.
/// Branches live only here — school schemas reference them by foreign key rather than
/// keeping a mirrored copy.
/// </summary>
public class Branch : AuditableEntity
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public BranchStatus Status { get; set; }

    /// <summary>At most one default branch per school, enforced by a partial unique index.</summary>
    public bool IsDefault { get; set; }

    /// <summary>IANA identifier, e.g. "Asia/Karachi". Required: all times are stored UTC and displayed here.</summary>
    public string TimeZone { get; set; } = null!;

    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public School School { get; set; } = null!;
}
