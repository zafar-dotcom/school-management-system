using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Master;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("audit_log");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();

        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.Changes).HasColumnType("jsonb");
        b.Property(x => x.IpAddress).HasColumnType("inet");
        b.Property(x => x.UserAgent).HasMaxLength(400);

        b.HasIndex(x => x.OccurredAt).IsDescending();
        b.HasIndex(x => new { x.EntityType, x.EntityId });

        // No foreign keys, deliberately: an audit record must outlive what it describes.
    }
}
