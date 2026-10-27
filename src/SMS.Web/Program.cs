using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SMS.Domain.Master;
using SMS.Infrastructure;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Setup;
using SMS.Web.Tenancy;

var builder = WebApplication.CreateBuilder(args);

// Serilog. School and branch are added to the log context per request below, so a line in
// a support bundle can be traced to a tenant — engineering practices, architecture s10.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "sms-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

// Persistence, tenant context, clock, audit interceptor, schema check.
builder.Services.AddInfrastructure(builder.Configuration);

// Identity: credentials only. AddIdentityCore deliberately, not AddIdentity — the latter
// brings role services, and roles are not the authorization source of truth here
// (architecture s5.2). MasterDbContext derives from IdentityUserContext, so the role tables
// do not exist to be read even by mistake.
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<MasterDbContext>()
    .AddClaimsPrincipalFactory<SchoolClaimsPrincipalFactory>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "sms.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Denied";
});

// A dismissed teacher must lose access in minutes, not when the cookie expires —
// architecture s5.6. This re-reads the security stamp, which also refreshes our claims.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

// Keys in the database, not on disk: cookies then survive a reinstall, and a second
// instance is already handled — architecture s8.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<MasterDbContext>()
    .SetApplicationName("SchoolManagementSystem");

builder.Services.AddControllersWithViews();

var app = builder.Build();

// The schema check runs once, before the first request is served. It never migrates:
// migrations are an installer step (architecture s11). A mismatch leaves the application
// running but serving only the maintenance page, so an operator can read the reason.
await using (var scope = app.Services.CreateAsyncScope())
{
    var state = scope.ServiceProvider.GetRequiredService<InstallationState>();
    var check = scope.ServiceProvider.GetRequiredService<SchemaVersionCheck>();
    var (status, detail) = await check.RunAsync();
    state.Set(status, detail);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.MapStaticAssets();

app.UseRouting();

// Order matters and is not arbitrary:
//   gate    - refuse outright if the schema is wrong; no point authenticating against it
//   authn   - who is this
//   tenant  - which school and branch, re-validated against master, never from the cookie
//   authz   - may they do this
app.UseMiddleware<InstallationGateMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
   .WithStaticAssets();

app.Run();
