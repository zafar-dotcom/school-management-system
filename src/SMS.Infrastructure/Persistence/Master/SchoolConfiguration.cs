using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Master;

public class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> b)
    {
        b.ToTable("school", t =>
        {
            t.HasCheckConstraint("ck_school_slug_format", "slug ~ '^[a-z0-9_]{1,40}$'");
            t.HasCheckConstraint("ck_school_schema_format", "schema_name ~ '^school_[a-z0-9_]{1,40}$'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.SchemaName).HasMaxLength(47).IsRequired();
        b.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
        b.Property(x => x.SchemaVersion).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.UseXminConcurrencyToken();

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.SchemaName).IsUnique();

        // Target of the composite foreign keys that bind branches and memberships to one school.
        b.HasAlternateKey(x => new { x.Id, x.Slug });
    }
}
