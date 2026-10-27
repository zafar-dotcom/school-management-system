using SMS.Application.Abstractions;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Tenancy;

/// <summary>
/// A tenant context for work that touches only schema <i>metadata</i> — the migrations
/// history, the model — and never reads or writes a branch-scoped row.
/// <para>
/// <b>Never use this to serve a request.</b> Its branch id is empty, so any query it
/// filtered would match nothing and any row it wrote would carry an invalid branch.
/// It exists so the startup schema check can build a <see cref="SchoolDbContext"/> before
/// a school or branch is known.
/// </para>
/// </summary>
public sealed class MetadataOnlyTenantContext : ITenantContext
{
    public Guid SchoolId => Guid.Empty;
    public string SchemaName => SchoolDbContext.DefaultSchema;
    public Guid ActiveBranchId => Guid.Empty;
    public Guid? UserId => null;
    public bool IsResolved => false;
}
