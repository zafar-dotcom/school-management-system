using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Master;

public class SchoolMembershipBranchConfiguration : IEntityTypeConfiguration<SchoolMembershipBranch>
{
    public void Configure(EntityTypeBuilder<SchoolMembershipBranch> b)
    {
        b.ToTable("school_membership_branch");

        b.HasKey(x => new { x.MembershipId, x.BranchId });

        // Both foreign keys are composite and include SchoolId. That is what makes it
        // impossible to insert a row scoping a School A membership to a School B branch —
        // enforced by the database, so no application check can forget it.
        b.HasOne(x => x.Membership)
         .WithMany(x => x.Branches)
         .HasForeignKey(x => new { x.MembershipId, x.SchoolId })
         .HasPrincipalKey(x => new { x.Id, x.SchoolId })
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Branch)
         .WithMany()
         .HasForeignKey(x => new { x.BranchId, x.SchoolId })
         .HasPrincipalKey(x => new { x.Id, x.SchoolId })
         .OnDelete(DeleteBehavior.Cascade);
    }
}
