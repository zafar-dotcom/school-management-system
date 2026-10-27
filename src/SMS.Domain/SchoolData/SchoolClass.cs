using SMS.Domain.Common;

namespace SMS.Domain.SchoolData;

/// <summary>
/// A class or grade within a branch — the first branch-scoped entity, and the one that
/// proves the isolation mechanism end to end.
/// <para>
/// Version 1 has a single branch, so the branch filter always matches. The column and the
/// filter exist anyway: adding them later would mean touching every table, query, index
/// and write path. See architecture section 1.3.
/// </para>
/// </summary>
public class SchoolClass : BranchScopedEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>Ordering for display, e.g. Grade 1 before Grade 2.</summary>
    public int SortOrder { get; set; }
}
