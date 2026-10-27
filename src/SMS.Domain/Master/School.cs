using SMS.Domain.Common;

namespace SMS.Domain.Master;

/// <summary>
/// A tenant. Version 1 has exactly one row; the structure supports many so that
/// multi-school remains an addition rather than a rewrite.
/// </summary>
public class School : AuditableEntity
{
    /// <summary>Immutable, lower-case, used to derive <see cref="SchemaName"/>.</summary>
    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// The PostgreSQL schema holding this school's data, e.g. "school_main".
    /// Interpolated into SET search_path when multi-school is activated, so the
    /// database carries a CHECK constraint on its format as well.
    /// </summary>
    public string SchemaName { get; set; } = null!;

    public SchoolStatus Status { get; set; }

    /// <summary>ISO 3166-1 alpha-2. Unused in version 1; present so the column need not be added later.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Latest migration applied to this school's schema. The app refuses to serve a mismatch.</summary>
    public string? SchemaVersion { get; set; }

    public DateTimeOffset? LastMigratedAt { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<SchoolMembership> Memberships { get; set; } = new List<SchoolMembership>();
}
