using Microsoft.EntityFrameworkCore;
using SMS.Application.Abstractions;
using SMS.Infrastructure.Setup;

namespace SMS.Infrastructure.Persistence.Stores;

/// <summary>
/// Reports the school-schema migration this build expects.
/// <para>
/// Computed once per instance: it reads the migrations compiled into the assembly, which
/// cannot change while the process is running.
/// </para>
/// </summary>
public sealed class SchemaVersionSource : ISchemaVersionSource
{
    private readonly Lazy<string> _version;

    public SchemaVersionSource(DbContextOptions<SchoolDbContext> options) =>
        _version = new Lazy<string>(() => SchemaVersionCheck.ExpectedSchoolSchemaVersion(options));

    public string ExpectedSchoolSchemaVersion => _version.Value;
}
