using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SMS.Application.Abstractions;
using SMS.Domain.Common;

namespace SMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps created/updated columns on every save.
/// <para>
/// Applied once, here, so no module has to remember. Field-level change capture into
/// audit_log is added alongside the first feature module — see architecture section 8.
/// </para>
/// </summary>
public class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public AuditingInterceptor(ITenantContext tenant, IClock clock)
    {
        _tenant = tenant;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;

        var now = _clock.UtcNow;
        var userId = _tenant.IsResolved ? _tenant.UserId : null;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    // Creation stamps are never rewritten by an update.
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;
                    break;
            }
        }

        // BranchId is assigned server-side from the resolved context, never bound from a
        // request. Architecture section 4.6, rules 1 and 2.
        foreach (var entry in context.ChangeTracker.Entries<BranchScopedEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.BranchId == Guid.Empty)
                entry.Entity.BranchId = _tenant.ActiveBranchId;
        }
    }
}
