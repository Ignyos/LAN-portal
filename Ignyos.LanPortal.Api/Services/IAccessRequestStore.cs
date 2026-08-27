using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Api.Services;

public enum AccessRequestStatus
{
    Pending,
    Approved,
    Denied,
    Expired
}

public sealed record AccessRequestRecord(
    Guid RequestId,
    string UserCode,
    string RequestedUserName,
    string DeviceName,
    string? SourceIp,
    string? UserAgent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    AccessRequestStatus Status,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionReason,
    string? ApprovedUserName,
    string? ApprovedRoles,
    int? ApprovedTokenMinutes,
    string? IssuedAccessToken,
    DateTimeOffset? IssuedAccessTokenExpiresAtUtc,
    string? IssuedRefreshToken,
    DateTimeOffset? IssuedRefreshTokenExpiresAtUtc);

public interface IAccessRequestStore
{
    AccessRequestRecord Create(
        Guid requestId,
        string requestedUserName,
        string deviceName,
        string? sourceIp,
        string? userAgent,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        string userCode,
        AccessHistoryRecord history);

    AccessRequestRecord? Get(Guid requestId, string userCode);

    IReadOnlyList<AccessRequestRecord> GetPending();

    bool Approve(Guid requestId, string userName, string roles, int? tokenMinutes, string deviceName, DateTimeOffset decidedAtUtc, AccessHistoryRecord history);

    bool Deny(Guid requestId, string? reason, DateTimeOffset decidedAtUtc, AccessHistoryRecord history);

    bool MarkExpired(Guid requestId, DateTimeOffset expiredAtUtc, AccessHistoryRecord history);

    IReadOnlyList<AccessRequestRecord> GetPendingExpired(DateTimeOffset nowUtc, int maxCount = 1000);

    bool SaveIssuedToken(
        Guid requestId,
        string accessToken,
        DateTimeOffset accessTokenExpiresAtUtc,
        string refreshToken,
        DateTimeOffset? refreshTokenExpiresAtUtc);

    int PurgeCompletedBefore(DateTimeOffset cutoffUtc);
}
