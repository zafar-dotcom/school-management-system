using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SMS.Api.Auth;
using SMS.Api.Contracts;
using SMS.Api.Tenancy;
using SMS.Application;
using SMS.Application.Access;
using SMS.Application.Classes;
using SMS.Domain.Master;
using SMS.Infrastructure;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "sms-api-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

// The same use cases the website runs. Both hosts call them in-process — this API is not
// a layer in front of SMS.Web, and SMS.Web never calls it over HTTP (architecture s11).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Credentials only, exactly as the website configures them — the shared policy is what
// stops the two hosts drifting to different password or lockout rules.
builder.Services
    .AddIdentityCore<AppUser>(IdentityPolicy.Configure)
    .AddEntityFrameworkStores<MasterDbContext>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddScoped<JwtTokenIssuer>();

// Resolved once, here, so the issuer and the validator are guaranteed to hold the same
// key — including the generated development one.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = SigningKeyProvider.Resolve(jwtOptions, builder.Environment);
builder.Services.AddSingleton(signingKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey.Key,
            ValidateLifetime = true,
            // No leeway. The default five minutes would keep a token alive past its stated
            // expiry, and expiry is one of the few things a client can rely on.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Same refusal as the website: never serve a schema this build does not match.
await using (var scope = app.Services.CreateAsyncScope())
{
    var state = scope.ServiceProvider.GetRequiredService<InstallationState>();
    var check = scope.ServiceProvider.GetRequiredService<SchemaVersionCheck>();
    var (status, detail) = await check.RunAsync();
    state.Set(status, detail);
}

if (signingKey.IsEphemeral)
{
    app.Logger.LogWarning(
        "No Jwt:SigningKey configured. Using an ephemeral development key — every token " +
        "issued will stop working when this process restarts.");
}

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseMiddleware<ApiTenantResolutionMiddleware>();
app.UseAuthorization();

// ---------------------------------------------------------------------------------------

app.MapGet("/health", (InstallationState state) => Results.Ok(new
{
    status = state.Status.ToString(),
    ready = state.IsReady,
    detail = state.Detail
}))
.AllowAnonymous()
.WithName("Health");

app.MapPost("/auth/token", async (
    TokenRequest request,
    AuthenticateHandler authenticate,
    JwtTokenIssuer issuer,
    InstallationState state,
    CancellationToken ct) =>
{
    if (!state.IsReady)
        return Results.Problem("The installation is not ready to serve.", statusCode: 503);

    var result = await authenticate.HandleAsync(
        new AuthenticateCommand(request.Email, request.Password), ct);

    if (!result.Succeeded)
    {
        // 401 with the handler's own message. It is deliberately the same text for a
        // missing account and a wrong password, so this does not reveal which.
        return Results.Json(
            new { error = "authentication_failed", detail = string.Join(" ", result.Errors) },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var (token, expiresAt) = issuer.Issue(result.Value.User, result.Value.ClaimSet);
    return Results.Ok(new TokenResponse(token, expiresAt));
})
.AllowAnonymous()
.WithName("IssueToken");

app.MapGet("/classes", async (ListClassesHandler list, CancellationToken ct) =>
{
    var result = await list.HandleAsync(ct);

    return Results.Ok(new ClassListResponse(
        result.SchoolName,
        result.BranchName,
        result.ActiveBranchId,
        [.. result.Classes.Select(c => new ClassResponse(c.Id, c.Code, c.Name, c.SortOrder))]));
})
.RequireAuthorization()
.WithName("ListClasses");

app.MapPost("/classes", async (
    CreateClassRequest request,
    CreateClassHandler create,
    CancellationToken ct) =>
{
    var result = await create.HandleAsync(
        new CreateClassCommand(request.Code, request.Name, request.SortOrder), ct);

    return result.Succeeded
        ? Results.Created($"/classes/{result.Value}", new { id = result.Value })
        : Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(CreateClassRequest.Code)] = [.. result.Errors]
        });
})
.RequireAuthorization()
.WithName("CreateClass");

app.Run();
