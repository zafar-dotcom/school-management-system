using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SMS.Infrastructure.Persistence.Master;

/// <summary>
/// Names the key-ring table in the singular, to match every other table in this schema.
/// The snake_case convention would otherwise pluralise the framework's type name.
/// </summary>
public class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> b)
    {
        b.ToTable("data_protection_key");
        b.HasKey(x => x.Id);
    }
}
