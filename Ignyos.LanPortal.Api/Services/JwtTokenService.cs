using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ignyos.LanPortal.Contracts;
using Microsoft.IdentityModel.Tokens;

namespace Ignyos.LanPortal.Api.Services;

public sealed class JwtTokenService(IAppSettingsStore settingsStore) : IJwtTokenService
{
    public (string AccessToken, DateTimeOffset ExpiresAtUtc, string Jti) CreateAccessToken(
        string userName,
        IEnumerable<string> roles,
        int accessTokenMinutes,
        string deviceName,
        IEnumerable<string>? permissions = null)
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

        var normalizedRoles = roles
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var role in normalizedRoles)
        {
            // Use the short "role" claim name so the client can extract it via
            // a plain JsonDocument lookup without relying on ClaimTypes URI mapping.
            claims.Add(new Claim("role", role));
        }

        var effectivePermissions = (permissions ?? GetDefaultPermissions(normalizedRoles))
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var permission in effectivePermissions)
        {
            // Emit permissions as repeated short-name claims (perm=...) instead of CSV.
            claims.Add(new Claim(PermissionClaimTypes.Permission, permission));
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

    private static IEnumerable<string> GetDefaultPermissions(IEnumerable<string> roles)
    {
        // Current baseline keeps role model and permission model distinct while
        // issuing an effective-permission snapshot in the token.
        var hasUserLikeRole = false;

        foreach (var role in roles)
        {
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return PermissionKeys.All;
            }

            if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            {
                hasUserLikeRole = true;
            }
        }

        if (hasUserLikeRole)
        {
            return
            [
                PermissionKeys.Read,
                PermissionKeys.NewFolder,
                PermissionKeys.Rename,
                PermissionKeys.Move,
                PermissionKeys.Delete,
                PermissionKeys.Upload,
                PermissionKeys.Download,
                PermissionKeys.Search
            ];
        }

        return [PermissionKeys.Read];
    }
}
