using Microsoft.EntityFrameworkCore;
using SMS.Application.Abstractions;
using SMS.Domain.SchoolData;

namespace SMS.Infrastructure.Persistence.Stores;

/// <summary>
/// <see cref="ISchoolStore"/> over <see cref="SchoolDbContext"/>.
/// <para>
/// Every query below is narrowed to the active branch by that context's global filter.
/// Nothing here adds a branch predicate, and nothing should: if the filter were removed,
/// these methods would start returning other branches' rows and no line in this file
/// would look wrong. That is the trade the filter makes — one place to get right.
/// </para>
/// </summary>
public sealed class SchoolStore : ISchoolStore
{
    private readonly SchoolDbContext _db;

    public SchoolStore(SchoolDbContext db) => _db = db;

    public async Task<IReadOnlyList<SchoolClass>> ListClassesAsync(CancellationToken ct = default) =>
        await _db.Classes
                 .AsNoTracking()
                 .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
                 .ToListAsync(ct);

    public Task<bool> ClassCodeExistsAsync(string code, CancellationToken ct = default) =>
        _db.Classes.AnyAsync(c => c.Code == code, ct);

    public void Add(SchoolClass schoolClass) => _db.Classes.Add(schoolClass);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
