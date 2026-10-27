using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Master;

public class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> b)
    {
        b.ToTable("license", t =>
            t.HasCheckConstraint("ck_license_dates",
                "expires_at > starts_at AND grace_until >= expires_at"));

        b.HasKey(x => x.Id);
        b.Property(x => x.LicenseNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.LicensedTo).HasMaxLength(200).IsRequired();
        b.Property(x => x.SignedPayload).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.UseXminConcurrencyToken();

        b.HasIndex(x => x.LicenseNumber).IsUnique();

        b.HasOne(x => x.School)
         .WithMany()
         .HasForeignKey(x => x.SchoolId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
