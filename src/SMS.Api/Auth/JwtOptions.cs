namespace SMS.Api.Auth;

/// <summary>
/// Token settings. Bound from the <c>Jwt</c> configuration section.
/// <para>
/// <see cref="SigningKey"/> is a secret and belongs in user secrets or the installer's
/// protected configuration — never in a committed file.
/// </para>
/// </summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "sms";
    public string Audience { get; set; } = "sms-clients";
    public string? SigningKey { get; set; }

    /// <summary>
    /// Short by design. Mobile and desktop clients re-authenticate until refresh tokens
    /// exist — that design waits for a real client, per architecture section 11.
    /// </summary>
    public int LifetimeMinutes { get; set; } = 60;
}
