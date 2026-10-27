namespace SMS.Application.Abstractions;

/// <summary>
/// The school-schema migration this build expects, so setup can stamp
/// <c>school.schema_version</c> without the application layer knowing what EF Core is.
/// </summary>
public interface ISchemaVersionSource
{
    string ExpectedSchoolSchemaVersion { get; }
}
