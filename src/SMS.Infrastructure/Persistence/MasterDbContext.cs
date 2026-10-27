using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence;

/// <summary>
/// The platform schema: schools, branches, logins, memberships, licence and audit.
/// <para>
/// Derives from <see cref="IdentityUserContext{TUser,TKey}"/> rather than IdentityDbContext
/// so that Identity's role tables are never created. Authorization comes from
/// <see cref="SchoolMembership"/> — see architecture section 5.2. Removing the tables makes
/// that a structural fact rather than a rule someone has to remember.
/// </para>
/// </summary>
public class MasterDbContext : IdentityUserContext<AppUser, Guid>, IDataProtectionKeyContext
{
    public const string Schema = "master";

    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

    public DbSet<School> Schools => Set<School>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<SchoolMembership> Memberships => Set<SchoolMembership>();
    public DbSet<SchoolMembershipBranch> MembershipBranches => Set<SchoolMembershipBranch>();
    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    /// <summary>
    /// The ASP.NET Core data-protection key ring. Held in the database rather than on the
    /// filesystem so authentication cookies survive a reinstall and a second instance is
    /// already catered for — architecture section 8. Shape is fixed by the framework.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(
            typeof(MasterDbContext).Assembly,
            t => t.Namespace?.EndsWith(".Persistence.Master", StringComparison.Ordinal) == true);
    }
}
