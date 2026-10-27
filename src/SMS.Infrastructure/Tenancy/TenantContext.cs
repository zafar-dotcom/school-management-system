using SMS.Application.Abstractions;

namespace SMS.Infrastructure.Tenancy;

/// <summary>
/// The scoped, mutable tenant context for one request or one unit of background work.
/// <para>
/// It starts <b>unresolved</b> and stays that way until something validates the caller and
/// calls <see cref="Resolve"/>. Reading school or branch before then throws rather than
/// returning a default, because a silent <see cref="Guid.Empty"/> would be written into
/// rows as a real branch id and would quietly defeat the filter in
/// <see cref="Persistence.SchoolDbContext"/>. Failing loudly is the point.
/// </para>
/// <para>
/// Version 1 always resolves to the one school and its Main branch. Multi-school changes
/// what gets passed to <see cref="Resolve"/> and nothing else — architecture section 4.5.
/// </para>
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private State? _state;

    public bool IsResolved => _state is not null;

    public Guid SchoolId => Resolved.SchoolId;
    public string SchemaName => Resolved.SchemaName;
    public Guid ActiveBranchId => Resolved.ActiveBranchId;

    /// <summary>Null for background or setup work with no signed-in user. Does not throw.</summary>
    public Guid? UserId => _state?.UserId;

    private State Resolved => _state ?? throw new InvalidOperationException(
        "The tenant context has not been resolved for this scope. School-scoped services must " +
        "not be used before authentication and tenant resolution have run, and background work " +
        "must call Resolve explicitly rather than relying on an ambient value.");

    /// <summary>
    /// Called once per scope, after the caller's membership has been validated against
    /// <c>master</c>. The schema name must come from the validated School record, never from
    /// a claim or anything else carried in the request.
    /// </summary>
    public void Resolve(Guid schoolId, string schemaName, Guid activeBranchId, Guid? userId)
    {
        if (schoolId == Guid.Empty)
            throw new ArgumentException("School id is required.", nameof(schoolId));
        if (activeBranchId == Guid.Empty)
            throw new ArgumentException("Active branch id is required.", nameof(activeBranchId));
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException("Schema name is required.", nameof(schemaName));

        _state = new State(schoolId, schemaName, activeBranchId, userId);
    }

    private sealed record State(Guid SchoolId, string SchemaName, Guid ActiveBranchId, Guid? UserId);
}
