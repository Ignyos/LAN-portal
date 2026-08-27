using System.Collections.Concurrent;
using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Api.Services;

public sealed class InMemoryDeviceLoginStore(
    IAppSettingsStore settingsStore,
    IAccessRequestStore accessRequestStore,
    ApplicationEventLogger applicationEventLogger) : IDeviceLoginStore
{
    private const int MaxDecisionHistory = 100;
    private readonly ConcurrentQueue<LoginDecisionDto> decisions = new();

    public DeviceLoginStartResponseDto CreateRequest(
        string requestedUserName,
        string deviceName,
        string? sourceIp,
        string? userAgent)
    {
        var requestId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAtUtc = now.AddSeconds(settingsStore.GetAccessRequestTimeoutSeconds());
        var request = accessRequestStore.Create(
            requestId,
            requestedUserName,
            deviceName,
            sourceIp,
            userAgent,
            now,
            expiresAtUtc,
            BuildUserCode(),
            new AccessHistoryRecord(Guid.NewGuid(), $"request:{requestId}:requested", AccessHistoryEventTypes.AccessRequested, null, null, requestedUserName, deviceName, null, null, now, now));

        applicationEventLogger.LogAccessRequestCreated(requestId, requestedUserName, deviceName, sourceIp);

        var pollIntervalSeconds = settingsStore.GetAccessRequestPollIntervalSeconds();
        return new DeviceLoginStartResponseDto(request.RequestId, request.UserCode, request.ExpiresAtUtc, pollIntervalSeconds);
    }

    public IReadOnlyList<PendingLoginRequestDto> GetPendingRequests()
        => accessRequestStore.GetPending()
            .OrderBy(request => request.CreatedAtUtc)
            .Select(request => new PendingLoginRequestDto(
                request.RequestId,
                request.RequestedUserName,
                request.DeviceName,
                request.SourceIp,
                request.UserAgent,
                request.CreatedAtUtc,
                request.ExpiresAtUtc))
            .ToArray();

    public DeviceLoginPollSnapshot Poll(Guid requestId, string userCode)
    {
        var request = accessRequestStore.Get(requestId, userCode);
        if (request is null)
        {
            return new DeviceLoginPollSnapshot("not_found", "Request not found.");
        }

        if (request.Status == AccessRequestStatus.Pending && request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            accessRequestStore.MarkExpired(
                request.RequestId,
                request.ExpiresAtUtc,
                new AccessHistoryRecord(Guid.NewGuid(), $"request:{request.RequestId}:expired", AccessHistoryEventTypes.AccessRequestExpired, request.RequestId, null, request.RequestedUserName, request.DeviceName, null, "Access request expired.", request.ExpiresAtUtc, DateTimeOffset.UtcNow));

            applicationEventLogger.LogAccessRequestExpired(request.RequestId, request.RequestedUserName, request.DeviceName);

            return new DeviceLoginPollSnapshot("expired", "Request expired.");
        }

        return request.Status switch
        {
            AccessRequestStatus.Pending => new DeviceLoginPollSnapshot("pending", "Waiting for approval."),
            AccessRequestStatus.Denied => new DeviceLoginPollSnapshot(
                "denied",
                string.IsNullOrWhiteSpace(request.DecisionReason) ? "Request denied." : request.DecisionReason),
            AccessRequestStatus.Approved when !string.IsNullOrWhiteSpace(request.IssuedAccessToken) && request.IssuedAccessTokenExpiresAtUtc is not null
                => new DeviceLoginPollSnapshot(
                    "approved",
                    "Approved.",
                    DeviceName: request.DeviceName,
                    ExistingAccessToken: request.IssuedAccessToken,
                    ExistingAccessTokenExpiresAtUtc: request.IssuedAccessTokenExpiresAtUtc,
                    ExistingRefreshToken: request.IssuedRefreshToken,
                    ExistingRefreshTokenExpiresAtUtc: request.IssuedRefreshTokenExpiresAtUtc),
            AccessRequestStatus.Approved => new DeviceLoginPollSnapshot(
                "approved",
                "Approved.",
                RequestId: request.RequestId,
                DeviceName: request.DeviceName,
                UserName: request.ApprovedUserName,
                Roles: request.ApprovedRoles?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                TokenMinutes: request.ApprovedTokenMinutes),
            AccessRequestStatus.Expired => new DeviceLoginPollSnapshot("expired", "Request expired."),
            _ => new DeviceLoginPollSnapshot("error", "Unknown request state.")
        };
    }

    public bool Approve(Guid requestId, string userName, string roles, int? tokenMinutes, string? deviceName = null)
    {
        var request = accessRequestStore.Get(requestId, FindUserCode(requestId));
        if (request is null || request.Status != AccessRequestStatus.Pending || request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var approved = accessRequestStore.Approve(
            requestId,
            userName,
            roles,
            tokenMinutes,
            string.IsNullOrWhiteSpace(deviceName) ? request.DeviceName : deviceName.Trim(),
            DateTimeOffset.UtcNow,
            new AccessHistoryRecord(Guid.NewGuid(), $"request:{requestId}:approved", AccessHistoryEventTypes.AccessApproved, requestId, null, userName, string.IsNullOrWhiteSpace(deviceName) ? request.DeviceName : deviceName.Trim(), roles, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        if (!approved)
        {
            return false;
        }

        applicationEventLogger.LogAccessRequestApproved(requestId, userName, string.IsNullOrWhiteSpace(deviceName) ? request.DeviceName : deviceName.Trim(), roles);

        var updated = accessRequestStore.Get(requestId, request.UserCode)!;
        var now = DateTimeOffset.UtcNow;
        decisions.Enqueue(new LoginDecisionDto(requestId, updated.DeviceName, "approved", userName, roles, null, now));
        TrimDecisions();
        return true;
    }

    public bool Deny(Guid requestId, string? reason)
    {
        var request = accessRequestStore.Get(requestId, FindUserCode(requestId));
        if (request is null || request.Status != AccessRequestStatus.Pending || request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var denied = accessRequestStore.Deny(requestId, reason, now,
            new AccessHistoryRecord(Guid.NewGuid(), $"request:{requestId}:denied", AccessHistoryEventTypes.AccessDenied, requestId, null, request.RequestedUserName, request.DeviceName, null, reason, now, now));
        if (!denied)
        {
            return false;
        }

        applicationEventLogger.LogAccessRequestDenied(requestId, request.RequestedUserName, request.DeviceName, reason);

        decisions.Enqueue(new LoginDecisionDto(requestId, request.DeviceName, "denied", null, null, reason, now));
        TrimDecisions();
        return true;
    }

    public void SaveIssuedToken(Guid requestId, string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset? refreshTokenExpiresAtUtc)
        => accessRequestStore.SaveIssuedToken(requestId, accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc);

    public IReadOnlyList<LoginDecisionDto> GetRecentDecisions(int maxCount = 25)
        => maxCount <= 0 ? [] : decisions.Reverse().Take(maxCount).ToArray();

    public void RecordLogoutEvent(string deviceName, string userName, string? roles)
    {
        decisions.Enqueue(new LoginDecisionDto(Guid.NewGuid(), deviceName, "logout", userName, roles, "User logged out.", DateTimeOffset.UtcNow));
        TrimDecisions();
    }

    private string FindUserCode(Guid requestId)
        => accessRequestStore.GetPending().FirstOrDefault(request => request.RequestId == requestId)?.UserCode
            ?? string.Empty;

    private static string BuildUserCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> code = stackalloc char[7];
        for (var i = 0; i < code.Length; i++) code[i] = i == 3 ? '-' : chars[Random.Shared.Next(chars.Length)];
        return new string(code);
    }

    private void TrimDecisions()
    {
        while (decisions.Count > MaxDecisionHistory) decisions.TryDequeue(out _);
    }
}
