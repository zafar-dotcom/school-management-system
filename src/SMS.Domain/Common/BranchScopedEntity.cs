namespace SMS.Domain.Common;

/// <summary>
/// Base for school-schema entities that belong to a single branch.
/// <para>
/// <b>BranchId is never set from a request.</b> It is assigned server-side from the
/// resolved tenant context, and every query against these entities carries a global
/// filter on it. See architecture document section 4.6.
/// </para>
/// </summary>
public abstract class BranchScopedEntity : AuditableEntity
{
    public Guid BranchId { get; set; }
}
