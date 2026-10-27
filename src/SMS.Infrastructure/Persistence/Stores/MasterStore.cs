using Microsoft.EntityFrameworkCore;
using SMS.Application.Abstractions;
using SMS.Domain.Common;
using SMS.Domain.Master;

namespace SMS.Infrastructure.Persistence.Stores;

/// <summary>
/// <see cref="IMasterStore"/> over <see cref="MasterDbContext"/>.
/// <para>
/// Reads are <c>AsNoTracking</c> and projected to small records: these run on every
/// request during tenant resolution, and there is no reason to materialise whole entities
/// or keep them in the change tracker.
/// </para>
/// </summary>
public sealed class MasterStore : IMasterStore
{
    private readonly MasterDbContext _db;

    public MasterStore(MasterDbContext db) => _db = db;

    public Task<bool> AnySchoolExistsAsync(CancellationToken ct = default) =>
        _db.Schools.AnyAsync(ct);

    public void Add(School school) => _db.Schools.Add(school);

    public void Add(Branch branch) => _db.Branches.Add(branch);

    public void Add(SchoolMembership membership) => _db.Memberships.Add(membership);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<SchoolMembership?> FindActiveMembershipAsync(Guid userId, CancellationToken ct = default) =>
        _db.Memberships
           .AsNoTracking()
           .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
           .OrderBy(m => m.CreatedAt)
           .FirstOrDefaultAsync(ct);

    public async Task<Guid?> FindLandingBranchIdAsync(
        SchoolMembership membership, CancellationToken ct = default)
    {
        if (membership.BranchScope == BranchScope.AllBranches)
        {
            return await _db.Branches
                .AsNoTracking()
                .Where(b => b.SchoolId == membership.SchoolId
                         && b.IsDefault
                         && b.Status == BranchStatus.Active)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(ct);
        }

        return await _db.MembershipBranches
            .AsNoTracking()
            .Where(mb => mb.MembershipId == membership.Id)
            .Join(_db.Branches.Where(b => b.Status == BranchStatus.Active),
                  mb => mb.BranchId, b => b.Id, (mb, b) => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<SchoolRecord?> FindSchoolAsync(Guid schoolId, CancellationToken ct = default) =>
        _db.Schools
           .AsNoTracking()
           .Where(s => s.Id == schoolId)
           .Select(s => new SchoolRecord(s.Id, s.Name, s.SchemaName, s.Status))
           .FirstOrDefaultAsync(ct);

    public Task<MembershipRecord?> FindMembershipAsync(
        Guid userId, Guid schoolId, CancellationToken ct = default) =>
        _db.Memberships
           .AsNoTracking()
           .Where(m => m.UserId == userId && m.SchoolId == schoolId)
           .Select(m => new MembershipRecord(
               m.Id, m.Status, m.MembershipVersion, m.BranchScope, m.Role))
           .FirstOrDefaultAsync(ct);

    public Task<BranchRecord?> FindBranchAsync(Guid branchId, CancellationToken ct = default) =>
        _db.Branches
           .AsNoTracking()
           .Where(b => b.Id == branchId)
           .Select(b => new BranchRecord(b.Id, b.SchoolId, b.Status))
           .FirstOrDefaultAsync(ct);

    public Task<bool> IsBranchInScopeAsync(
        Guid membershipId, Guid branchId, CancellationToken ct = default) =>
        _db.MembershipBranches
           .AsNoTracking()
           .AnyAsync(mb => mb.MembershipId == membershipId && mb.BranchId == branchId, ct);

    public Task<ContextNames?> FindContextNamesAsync(Guid branchId, CancellationToken ct = default) =>
        _db.Branches
           .AsNoTracking()
           .Where(b => b.Id == branchId)
           .Select(b => new ContextNames(b.School.Name, b.Name))
           .FirstOrDefaultAsync(ct);
}
