namespace SMS.Application.Abstractions;

/// <summary>
/// The resolved school and branch for the current unit of work.
/// <para>
/// Version 1 always resolves to the single school and its Main branch, but it is
/// <b>resolved</b>, never hardcoded — so activating multi-school or multi-branch changes
/// how this is populated and nothing else.
/// </para>
/// <para>
/// Background work has no HTTP request and therefore no ambient context. School and branch
/// must be passed explicitly into application services there, never read from a request
/// scope. See architecture section 8.
/// </para>
/// </summary>
public interface ITenantContext
{
    Guid SchoolId { get; }

    /// <summary>The PostgreSQL schema holding this school's data, e.g. "school_main".</summary>
    string SchemaName { get; }

    /// <summary>The branch the user is currently working in. Every write takes its BranchId from here.</summary>
    Guid ActiveBranchId { get; }

    /// <summary>Null for system or background operations with no signed-in user.</summary>
    Guid? UserId { get; }

    bool IsResolved { get; }
}
