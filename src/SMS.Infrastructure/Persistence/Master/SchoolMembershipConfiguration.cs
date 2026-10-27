using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Master;

public class SchoolMembershipConfiguration : IEntityTypeConfiguration<SchoolMembership>
{
    public void Configure(EntityTypeBuilder<SchoolMembership> b)
    {
        b.ToTable("school_membership");

        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.BranchScope).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.MembershipVersion).IsRequired();

        b.UseXminConcurrencyToken();

        // A user may hold more than one role at a school — teacher and accountant, say —
        // so the role is part of the key. Branch scope is therefore per role, which is correct.
        b.HasIndex(x => new { x.UserId, x.SchoolId, x.Role }).IsUnique();

        b.HasAlternateKey(x => new { x.Id, x.SchoolId });

        b.HasOne(x => x.User)
         .WithMany(x => x.Memberships)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.School)
         .WithMany(x => x.Memberships)
         .HasForeignKey(x => x.SchoolId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
