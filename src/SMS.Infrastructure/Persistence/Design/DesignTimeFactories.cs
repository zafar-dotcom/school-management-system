using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SMS.Application.Abstractions;

namespace SMS.Infrastructure.Persistence.Design;

/// <summary>
/// Used only by "dotnet ef" at design time.
/// <para>
/// Creating a migration never opens a connection; applying one does. The connection string
/// comes from user secrets so no password is ever written into a file in the repository.
/// Set it with:
/// <code>dotnet user-secrets set "ConnectionStrings:Owner" "..." --project src/SMS.Infrastructure</code>
/// </para>
/// <para>
/// Note this uses the <c>sms_owner</c> role, not the application role: migrations change
/// the schema, and the runtime role deliberately cannot.
/// </para>
/// </summary>
internal static class DesignTime
{
    public const string ConnectionName = "Owner";

    public static string ConnectionString()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DesignTime).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        return config.GetConnectionString(ConnectionName)
            // Well-formed placeholder so "migrations add" still works on a machine with no
            // secrets configured. Any command that actually connects will fail clearly.
            ?? "Host=localhost;Port=5432;Database=sms;Username=sms_owner;Password=not-configured";
    }
}

public class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
{
    public MasterDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseNpgsql(DesignTime.ConnectionString(),
                o => o.MigrationsHistoryTable("__ef_migrations_history", "master"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new MasterDbContext(options);
    }
}

public class SchoolDbContextFactory : IDesignTimeDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseNpgsql(DesignTime.ConnectionString(),
                o => o.MigrationsHistoryTable("__ef_migrations_history", "school_main"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SchoolDbContext(options, new DesignTimeTenantContext());
    }

    /// <summary>A placeholder tenant so the model can be built without a request.</summary>
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid SchoolId => Guid.Empty;
        public string SchemaName => SchoolDbContext.DefaultSchema;
        public Guid ActiveBranchId => Guid.Empty;
        public Guid? UserId => null;
        public bool IsResolved => false;
    }
}
