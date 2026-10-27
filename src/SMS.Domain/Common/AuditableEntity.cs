namespace SMS.Domain.Common;

/// <summary>
/// Base for every entity that records who created or last changed it.
/// Populated by the audit interceptor, never by hand.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
