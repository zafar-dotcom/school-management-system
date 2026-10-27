using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMS.Domain.Common;
using SMS.Domain.Master;
using SMS.Infrastructure.Persistence;

namespace SMS.Web.Tenancy;

/// <summary>
/// Adds the school and branch claims to the principal at sign-in, and again whenever
/// Identity refreshes it after a security-stamp change.
/// <para>
/// The role comes from <see cref="SchoolMembership"/>, never from Identity's role tables —
/// those are not created at all. Architecture section 5.2.
/// </para>
/// </summary>
public class SchoolClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser>
{
    private readonly MasterDbContext _master;

    public SchoolClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        IOptions<IdentityOptions> options,
        MasterDbContext master)
        : base(userManager, options)
    {
        _master = master;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var membership = await ActiveMembershipAsync(_master, user.Id);
        if (membership is null)
            return identity;   // No active membership: the sign-in path refuses this.

        var branchId = await ActiveBranchIdAsync(_master, membership);
        if (branchId is null)
            return identity;

        identity.AddClaim(new Claim(SchoolClaims.SchoolId, membership.SchoolId.ToString()));
        identity.AddClaim(new Claim(SchoolClaims.ActiveBranchId, branchId.Value.ToString()));
        identity.AddClaim(new Claim(SchoolClaims.BranchScope, membership.BranchScope.ToString()));
        identity.AddClaim(new Claim(SchoolClaims.SchoolRole, membership.Role.ToString()));
        identity.AddClaim(new Claim(SchoolClaims.MembershipVersion,
            membership.MembershipVersion.ToString()));

        return identity;
    }

    /// <summary>
    /// Version 1 has exactly one membership per user. When multi-school arrives this becomes
    /// "the membership for the school the user selected", and nothing else changes.
    /// </summary>
    internal static Task<SchoolMembership?> ActiveMembershipAsync(MasterDbContext master, Guid userId) =>
        master.Memberships
              .AsNoTracking()
              .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
              .OrderBy(m => m.CreatedAt)
              .FirstOrDefaultAsync();

    /// <summary>
    /// The branch the user lands in. All-branches scope starts at the school's default
    /// branch; a specific scope starts at the first branch they actually hold.
    /// </summary>
    internal static async Task<Guid?> ActiveBranchIdAsync(MasterDbContext master, SchoolMembership membership)
    {
        if (membership.BranchScope == BranchScope.AllBranches)
        {
            var defaultBranch = await master.Branches
                .AsNoTracking()
                .Where(b => b.SchoolId == membership.SchoolId
                         && b.IsDefault
                         && b.Status == BranchStatus.Active)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync();

            return defaultBranch;
        }

        return await master.MembershipBranches
            .AsNoTracking()
            .Where(mb => mb.MembershipId == membership.Id)
            .Join(master.Branches.Where(b => b.Status == BranchStatus.Active),
                  mb => mb.BranchId, b => b.Id, (mb, b) => (Guid?)b.Id)
            .FirstOrDefaultAsync();
    }
}
