using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SMS.Api.Auth;

/// <summary>The signing key, and whether it was generated rather than configured.</summary>
public sealed record SigningKey(SymmetricSecurityKey Key, bool IsEphemeral);

public static class SigningKeyProvider
{
    /// <summary>
    /// Resolves the symmetric signing key once, at startup.
    /// <para>
    /// Outside Development a key <b>must</b> be configured: without one, tokens could not
    /// survive a restart or be validated by a second instance. In Development a random key
    /// is generated so the API runs with no setup, at the cost of invalidating every token
    /// when the process restarts — the right trade for a developer, the wrong one for a
    /// school, which is why it is refused anywhere else.
    /// </para>
    /// </summary>
    public static SigningKey Resolve(JwtOptions options, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(options.SigningKey))
        {
            var bytes = Encoding.UTF8.GetBytes(options.SigningKey);

            if (bytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey must be at least 32 bytes for HMAC-SHA256. Generate one " +
                    "and store it in user secrets, never in a committed file.");
            }

            return new SigningKey(new SymmetricSecurityKey(bytes), IsEphemeral: false);
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it in user secrets, or in the " +
                "installer's protected configuration, before running outside Development.");
        }

        return new SigningKey(
            new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), IsEphemeral: true);
    }
}
