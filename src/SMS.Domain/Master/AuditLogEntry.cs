using System.Net;
using SMS.Domain.Common;

namespace SMS.Domain.Master;

/// <summary>
/// Platform-level audit trail, written by the SaveChanges interceptor.
/// <para>
/// Deliberately carries no foreign keys: an audit record must survive deletion of
/// whatever it describes. The identifiers here are recorded values, not references.
/// </para>
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? UserId { get; set; }
    public Guid? SchoolId { get; set; }
    public Guid? BranchId { get; set; }

    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public AuditAction Action { get; set; }

    /// <summary>Changed values as jsonb. PostgreSQL queries this natively.</summary>
    public string? Changes { get; set; }

    /// <summary>Stored as PostgreSQL <c>inet</c>, which validates the address on write.</summary>
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
