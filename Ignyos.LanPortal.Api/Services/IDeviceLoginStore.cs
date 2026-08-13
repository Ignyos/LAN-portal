using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Api.Services;

public interface IDeviceLoginStore
{
    DeviceLoginStartResponseDto CreateRequest(
        string requestedUserName,
        string deviceName,
        string? sourceIp,
        string? userAgent);

    IReadOnlyList<PendingLoginRequestDto> GetPendingRequests();

    DeviceLoginPollSnapshot Poll(Guid requestId, string userCode);

    bool Approve(Guid requestId, string userName, string roles, int? tokenMinutes);

    bool Deny(Guid requestId, string? reason);

    void SaveIssuedToken(
        Guid requestId,
        string accessToken,
        DateTimeOffset accessTokenExpiresAtUtc,
        string refreshToken,
        DateTimeOffset? refreshTokenExpiresAtUtc);

    IReadOnlyList<LoginDecisionDto> GetRecentDecisions(int maxCount = 25);

    void RecordLogoutEvent(string deviceName, string userName, string? roles);
}

public sealed record LoginDecisionDto(
    Guid RequestId,
    string DeviceName,
    string Decision,
    string? UserName,
    string? Roles,
    string? Reason,
    DateTimeOffset DecidedAtUtc);

public sealed record DeviceLoginPollSnapshot(
    string Status,
    string? Message,
    Guid? RequestId = null,
    string? DeviceName = null,
    string? UserName = null,
    string[]? Roles = null,
    int? TokenMinutes = null,
    string? ExistingAccessToken = null,
    DateTimeOffset? ExistingAccessTokenExpiresAtUtc = null,
    string? ExistingRefreshToken = null,
    DateTimeOffset? ExistingRefreshTokenExpiresAtUtc = null);
