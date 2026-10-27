using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SMS.Application.Abstractions;
using SMS.Infrastructure.Setup;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Persistence.Interceptors;
using SMS.Infrastructure.Persistence.Stores;
using SMS.Infrastructure.Tenancy;
using SMS.Infrastructure.Time;

namespace SMS.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence for the running application.
    /// <para>
    /// Connects as the <b>application</b> role, which can read and write data but cannot
    /// change the schema. Migrations use a different, more privileged connection and are a
    /// deliberate installer step — never run from here.
    /// </para>
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("App")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:App is not configured. Set it in user secrets — see " +
                "src/SMS.Infrastructure/secrets.example.json. A password containing a semicolon " +
                "must be wrapped in double quotes or the connection string will not parse.");

        // One data source for the process; one connection per scope, taken from it.
        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
        services.AddScoped(sp => sp.GetRequiredService<NpgsqlDataSource>().CreateConnection());

        services.AddSingleton<IClock, SystemClock>();

        // One instance per scope, reached through either type. Middleware resolves it;
        // everything else consumes the read-only interface.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<AuditingInterceptor>();

        // Both contexts share the scoped connection, which is what makes one transaction
        // across both schemas possible.
        //
        // AddDbContext, deliberately not AddDbContextPool: SchoolDbContext captures the
        // active branch in its constructor, so a pooled instance would carry the previous
        // request's branch into the next one.
        services.AddDbContext<MasterDbContext>((sp, options) =>
            options.UseNpgsql(
                       sp.GetRequiredService<NpgsqlConnection>(),
                       o => o.MigrationsHistoryTable("__ef_migrations_history", MasterDbContext.Schema))
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>()));

        services.AddDbContext<SchoolDbContext>((sp, options) =>
            options.UseNpgsql(
                       sp.GetRequiredService<NpgsqlConnection>(),
                       o => o.MigrationsHistoryTable("__ef_migrations_history", SchoolDbContext.DefaultSchema))
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>()));

        // Lets CrossSchemaTransaction defer asking for the school context until it knows a
        // tenant has been resolved.
        services.AddScoped<Func<SchoolDbContext>>(sp => sp.GetRequiredService<SchoolDbContext>);
        services.AddScoped<CrossSchemaTransaction>();

        // The contracts the application layer declares, implemented here. This is the only
        // place the two sides are joined — SMS.Application never references this project.
        services.AddScoped<IMasterStore, MasterStore>();
        services.AddScoped<ISchoolStore, SchoolStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISchemaVersionSource, SchemaVersionSource>();

        services.AddSingleton<InstallationState>();
        services.AddScoped<SchemaVersionCheck>();

        return services;
    }
}
