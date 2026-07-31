using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Ignyos.LanPortal.Api.Services;

public sealed class JwtTokenService(IAppSettingsStore settingsStore) : IJwtTokenService
{
    public (string AccessToken, DateTimeOffset ExpiresAtUtc, string Jti) CreateAccessToken(string userName, IEnumerable<string> roles, int accessTokenMinutes, string deviceName)
    {
        var options = settingsStore.GetJwtConfig();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured in SQLite settings.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(options.SigningKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt signing key must be at least 32 bytes.");
        }

        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(accessTokenMinutes);

        var jti = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userName),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new("device_name", deviceName),
            new(JwtRegisteredClaimNames.Jti, jti)
        };

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Use the short "role" claim name so the client can extract it via
            // a plain JsonDocument lookup without relying on ClaimTypes URI mapping.
            claims.Add(new Claim("role", role));
        }

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires, jti);
    }
}
