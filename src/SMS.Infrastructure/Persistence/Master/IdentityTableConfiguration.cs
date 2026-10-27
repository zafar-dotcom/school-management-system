using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SMS.Infrastructure.Persistence.Master;

/// <summary>
/// Identity's side tables default to names like "AspNetUserClaims", which the snake_case
/// convention leaves alone because Identity sets them itself. Renamed here so the whole
/// schema reads consistently and nothing needs quoting in psql.
/// <para>
/// Note there are no role tables to rename — see <see cref="MasterDbContext"/>.
/// </para>
/// </summary>
public class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> b) => b.ToTable("app_user_claim");
}

public class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> b) => b.ToTable("app_user_login");
}

public class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> b) => b.ToTable("app_user_token");
}
