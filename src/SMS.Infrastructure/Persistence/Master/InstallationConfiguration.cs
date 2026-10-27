using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Master;

public class InstallationConfiguration : IEntityTypeConfiguration<Installation>
{
    public void Configure(EntityTypeBuilder<Installation> b)
    {
        b.ToTable("installation");

        b.HasKey(x => x.Id);
        b.Property(x => x.AppVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.MachineName).HasMaxLength(200);

        b.HasIndex(x => x.InstallationKey).IsUnique();
    }
}
