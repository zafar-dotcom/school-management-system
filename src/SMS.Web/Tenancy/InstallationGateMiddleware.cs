using SMS.Infrastructure.Setup;

namespace SMS.Web.Tenancy;

/// <summary>
/// Stands in front of everything and decides whether this build may serve at all.
/// <para>
/// A schema the code does not match is a refusal, not a warning — architecture section 4.8.
/// On-premise nobody is watching the logs, so serving a stale schema is how data gets
/// corrupted quietly.
/// </para>
/// </summary>
public class InstallationGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly InstallationState _state;

    public InstallationGateMiddleware(RequestDelegate next, InstallationState state)
    {
        _next = next;
        _state = state;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        switch (_state.Status)
        {
            case InstallationStatus.SchemaMismatch:
            case InstallationStatus.Unavailable:
            case InstallationStatus.Unknown:
                await WriteMaintenanceAsync(context);
                return;

            case InstallationStatus.NotInstalled:
                if (!path.StartsWithSegments("/Setup", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/Setup");
                    return;
                }
                break;

            case InstallationStatus.Ready:
                // Setup has run. Re-entering the wizard could create a second school.
                if (path.StartsWithSegments("/Setup", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/");
                    return;
                }
                break;
        }

        await _next(context);
    }

    private async Task WriteMaintenanceAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";

        var detail = _state.Detail ?? "The database could not be checked at startup.";
        await context.Response.WriteAsync(
            "School Management System is not available.\r\n\r\n" +
            detail + "\r\n\r\n" +
            "No data has been read or changed. Contact support with this message.\r\n");
    }
}
