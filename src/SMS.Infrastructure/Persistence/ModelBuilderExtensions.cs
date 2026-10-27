using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SMS.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Uses PostgreSQL's <c>xmin</c> system column as the optimistic concurrency token,
    /// so no version column is added to the table.
    /// <para>
    /// Npgsql's own <c>UseXminAsConcurrencyToken</c> helper was removed in the EF Core 10
    /// provider; this is the supported equivalent, kept in one place so every configuration
    /// gets the same behaviour.
    /// </para>
    /// </summary>
    public static EntityTypeBuilder<T> UseXminConcurrencyToken<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property<uint>("xmin")
               .HasColumnName("xmin")
               .IsRowVersion()
               .ValueGeneratedOnAddOrUpdate();

        return builder;
    }
}
