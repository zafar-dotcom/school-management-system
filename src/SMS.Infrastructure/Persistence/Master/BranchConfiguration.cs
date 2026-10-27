using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Master;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("branch");

        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        b.Property(x => x.AddressLine).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.UseXminConcurrencyToken();

        b.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();

        // Lets SchoolMembershipBranch key on (BranchId, SchoolId) and so prevents a
        // membership at one school being scoped to another school's branch.
        b.HasAlternateKey(x => new { x.Id, x.SchoolId });

        b.HasOne(x => x.School)
         .WithMany(x => x.Branches)
         .HasForeignKey(x => x.SchoolId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
