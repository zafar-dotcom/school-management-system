using Microsoft.Extensions.DependencyInjection;
using SMS.Application.Access;
using SMS.Application.Classes;
using SMS.Application.Setup;

namespace SMS.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use cases. Called by every host — the MVC site and the API both run
    /// these same handlers in-process, which is the whole point of them living here.
    /// <para>
    /// Scoped, because they depend on scoped stores and on the request's tenant context.
    /// </para>
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<BuildClaimSetHandler>();
        services.AddScoped<AuthenticateHandler>();
        services.AddScoped<ResolveTenantHandler>();
        services.AddScoped<CompleteSetupHandler>();
        services.AddScoped<ListClassesHandler>();
        services.AddScoped<CreateClassHandler>();

        return services;
    }
}
