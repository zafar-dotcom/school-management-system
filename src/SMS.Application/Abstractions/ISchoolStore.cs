using SMS.Domain.SchoolData;

namespace SMS.Application.Abstractions;

/// <summary>
/// One school's operational data.
/// <para>
/// Every read here is already narrowed to the active branch by the global query filter on
/// the underlying context — no caller adds a branch predicate, and none should.
/// </para>
/// </summary>
public interface ISchoolStore
{
    Task<IReadOnlyList<SchoolClass>> ListClassesAsync(CancellationToken ct = default);

    Task<bool> ClassCodeExistsAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// <c>BranchId</c> is left unset on purpose: it is stamped from the resolved tenant
    /// context on save, never taken from the caller — architecture section 4.6.
    /// </summary>
    void Add(SchoolClass schoolClass);

    Task SaveChangesAsync(CancellationToken ct = default);
}
