using Microsoft.AspNetCore.Identity;

namespace SMS.Application.Access;

/// <summary>
/// Password, lockout and account rules, in one place.
/// <para>
/// Both hosts configure Identity, and the same account must behave identically whichever
/// one it signs in through. Two copies of these numbers would drift — the API would quietly
/// end up with a weaker lockout than the website, and nobody would notice until it
/// mattered.
/// </para>
/// </summary>
public static class IdentityPolicy
{
    public static void Configure(IdentityOptions options)
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
    }
}
