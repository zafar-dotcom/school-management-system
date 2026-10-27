using Microsoft.EntityFrameworkCore;
using SMS.Application.Abstractions;
using SMS.Domain.SchoolData;

namespace SMS.Infrastructure.Persistence;

/// <summary>
/// One school's operational data. Every branch-scoped entity is filtered to the active branch.
/// </summary>
public class SchoolDbContext : DbContext
{
    /// <summary>
    /// Read from configuration in version 1. When multi-school is activated this is replaced
    /// by the resolved tenant's schema name, and no query changes.
    /// </summary>
    public const string DefaultSchema = "school_main";

    private readonly Guid _branchId;

    public SchoolDbContext(DbContextOptions<SchoolDbContext> options, ITenantContext tenant)
        : base(options)
    {
        // Captured into a field on purpose. EF caches the compiled model, so a filter that
        // closed over a local would bake the first request's branch into every later request.
        // Referencing an instance field makes EF parameterise it per query instead.
        _branchId = tenant.ActiveBranchId;
    }

    public DbSet<SchoolClass> Classes => Set<SchoolClass>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(DefaultSchema);

        builder.ApplyConfigurationsFromAssembly(
            typeof(SchoolDbContext).Assembly,
            t => t.Namespace?.EndsWith(".Persistence.SchoolSchema", StringComparison.Ordinal) == true);

        // The branch filter. Applied centrally so a new query cannot forget it.
        builder.Entity<SchoolClass>().HasQueryFilter(x => x.BranchId == _branchId);
    }
}
