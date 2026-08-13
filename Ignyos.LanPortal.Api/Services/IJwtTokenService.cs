namespace Ignyos.LanPortal.Api.Services;

public interface IJwtTokenService
{
    (string AccessToken, DateTimeOffset ExpiresAtUtc, string Jti) CreateAccessToken(
        string userName,
        IEnumerable<string> roles,
        int accessTokenMinutes,
        string deviceName,
        IEnumerable<string>? permissions = null);
}
