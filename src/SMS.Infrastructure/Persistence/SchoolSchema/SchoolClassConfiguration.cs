using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.SchoolData;

namespace SMS.Infrastructure.Persistence.SchoolSchema;

public class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> b)
    {
        b.ToTable("class");

        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.BranchId).IsRequired();

        b.UseXminConcurrencyToken();

        // BranchId leads the composite index so it is inert with one branch and becomes the
        // selective prefix the moment a second exists. No standalone BranchId index: with a
        // single branch it would have zero selectivity and cost on every write.
        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
    }
}
