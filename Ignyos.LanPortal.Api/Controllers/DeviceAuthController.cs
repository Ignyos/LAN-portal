using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Ignyos.LanPortal.Api.Services;
using Ignyos.LanPortal.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class DeviceAuthController(
    IDeviceLoginStore loginStore,
    IJwtTokenService jwtTokenService,
    IAppSettingsStore settingsStore) : ControllerBase
{
    private const int DefaultRefreshTokenMinutes = 60;
    private const int DefaultAccessTokenMinutes = 15;

    [HttpPost("request")]
    public ActionResult<DeviceLoginStartResponseDto> Start([FromBody] DeviceLoginStartRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return BadRequest("UserName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return BadRequest("DeviceName is required.");
        }

        var created = loginStore.CreateRequest(
            request.UserName.Trim(),
            request.DeviceName.Trim(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        return Ok(created);
    }

    [HttpPost("device/request")]
    public ActionResult<DeviceLoginStartResponseDto> StartDeviceRequest([FromBody] DeviceLoginStartRequestDto request)
        => Start(request);

    [HttpPost("poll")]
    public ActionResult<DeviceLoginPollResponseDto> Poll([FromBody] DeviceLoginPollRequestDto request)
    {
        var snapshot = loginStore.Poll(request.RequestId, request.UserCode);

        if (snapshot.Status == "approved")
        {
            if (!string.IsNullOrWhiteSpace(snapshot.ExistingAccessToken) &&
                snapshot.ExistingAccessTokenExpiresAtUtc is not null &&
                !string.IsNullOrWhiteSpace(snapshot.ExistingRefreshToken))
            {
                return Ok(new DeviceLoginPollResponseDto(
                    snapshot.Status,
                    snapshot.ExistingAccessToken,
                    snapshot.ExistingAccessTokenExpiresAtUtc,
                    snapshot.ExistingRefreshToken,
                    snapshot.ExistingRefreshTokenExpiresAtUtc,
                    snapshot.Message));
            }

            var userName = snapshot.UserName ?? "approved-user";
            var roles = snapshot.Roles is { Length: > 0 } ? snapshot.Roles : ["User"];
            var refreshTokenExpiresAtUtc = snapshot.TokenMinutes is > 0
                ? DateTimeOffset.UtcNow.AddMinutes(snapshot.TokenMinutes.Value)
                : (DateTimeOffset?)null;
            var accessTokenMinutes = DefaultAccessTokenMinutes;
            var deviceName = string.IsNullOrWhiteSpace(snapshot.DeviceName) ? "unknown-device" : snapshot.DeviceName;
            var sessionId = Guid.NewGuid();
            var refreshToken = CreateRefreshToken();
            var refreshTokenHash = ComputeRefreshTokenHash(refreshToken);

            var (accessToken, accessTokenExpiresAtUtc, jti) = jwtTokenService.CreateAccessToken(userName, roles, accessTokenMinutes, deviceName);

            settingsStore.RecordIssuedAccessSession(new AccessSessionRecord(
                SessionId: sessionId,
                Jti: jti,
                UserName: userName,
                DeviceName: deviceName,
                Roles: string.Join(',', roles),
                IssuedAtUtc: DateTimeOffset.UtcNow,
                ExpiresAtUtc: refreshTokenExpiresAtUtc,
                RevokedAtUtc: null,
                RevokedReason: null,
                LastSeenAtUtc: DateTimeOffset.UtcNow));
            settingsStore.UpsertRefreshToken(sessionId, refreshTokenHash, refreshTokenExpiresAtUtc);

            if (snapshot.RequestId is Guid requestId)
            {
                loginStore.SaveIssuedToken(requestId, accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc);
            }

            return Ok(new DeviceLoginPollResponseDto(
                snapshot.Status,
                accessToken,
                accessTokenExpiresAtUtc,
                refreshToken,
                refreshTokenExpiresAtUtc,
                snapshot.Message));
        }

        if (snapshot.Status == "pending")
        {
            return Accepted(new DeviceLoginPollResponseDto(snapshot.Status, null, null, null, null, snapshot.Message));
        }

        return Ok(new DeviceLoginPollResponseDto(snapshot.Status, null, null, null, null, snapshot.Message));
    }

    [HttpPost("device/poll")]
    public ActionResult<DeviceLoginPollResponseDto> PollDevice([FromBody] DeviceLoginPollRequestDto request)
        => Poll(request);

    [HttpPost("token/refresh")]
    public ActionResult<RefreshTokenResponseDto> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token is required.");
        }

        var refreshToken = request.RefreshToken.Trim();
        var refreshTokenHash = ComputeRefreshTokenHash(refreshToken);
        var session = settingsStore.GetActiveAccessSessionByRefreshTokenHash(refreshTokenHash);
        if (session is null)
        {
            return Unauthorized();
        }

        if (session.ExpiresAtUtc is DateTimeOffset sessionExpiresAtUtc && sessionExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Unauthorized();
        }

        var accessTokenMinutes = session.ExpiresAtUtc is DateTimeOffset boundedExpiry
            ? Math.Max(1, Math.Min(DefaultAccessTokenMinutes, (int)Math.Ceiling((boundedExpiry - DateTimeOffset.UtcNow).TotalMinutes)))
            : DefaultAccessTokenMinutes;
        var roles = session.Roles
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roles.Length == 0)
        {
            roles = ["User"];
        }

        var (accessToken, accessTokenExpiresAtUtc, newJti) = jwtTokenService.CreateAccessToken(
            session.UserName,
            roles,
            accessTokenMinutes,
            session.DeviceName);

        var updated = settingsStore.RefreshAccessSession(session.SessionId, refreshTokenHash, newJti, DateTimeOffset.UtcNow);
        if (!updated)
        {
            return Unauthorized();
        }

        return Ok(new RefreshTokenResponseDto(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            session.ExpiresAtUtc));
    }

    private static string CreateRefreshToken()
    {
        Span<byte> tokenBytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ComputeRefreshTokenHash(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return BadRequest("Token is missing jti claim.");
        }

        var revoked = settingsStore.RevokeAccessSessionByJti(jti, "User logged out.");
        if (revoked is not null)
        {
            loginStore.RecordLogoutEvent(revoked.DeviceName, revoked.UserName, revoked.Roles);
        }

        return Ok();
    }
}
