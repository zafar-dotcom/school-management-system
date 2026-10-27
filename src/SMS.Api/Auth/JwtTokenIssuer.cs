using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SMS.Application.Abstractions;
using SMS.Application.Access;
using SMS.Domain.Master;

namespace SMS.Api.Auth;

/// <summary>
/// Writes an authenticated identity into a bearer token.
/// <para>
/// This is the API's half of the seam described in architecture section 11: validating the
/// credentials is shared with the web host through <see cref="AuthenticateHandler"/>;
/// only <i>how the identity is persisted</i> differs, and that is this class.
/// </para>
/// </summary>
public sealed class JwtTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _key;
    private readonly IClock _clock;

    public JwtTokenIssuer(IOptions<JwtOptions> options, SigningKey signingKey, IClock clock)
    {
        _options = options.Value;
        _key = signingKey.Key;
        _clock = clock;
    }

    public (string Token, DateTimeOffset ExpiresAt) Issue(AppUser user, SchoolClaimSet claimSet)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.LifetimeMinutes);

        var claims = new List<Claim>
        {
            // NameIdentifier, not "sub": tenant resolution reads the same claim type in both
            // hosts, and ASP.NET Core maps "sub" to NameIdentifier inconsistently.
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        claims.AddRange(claimSet.ToClaims());

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

