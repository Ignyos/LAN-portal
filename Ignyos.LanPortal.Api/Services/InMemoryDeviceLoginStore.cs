using System.Collections.Concurrent;
using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Api.Services;

public sealed class InMemoryDeviceLoginStore : IDeviceLoginStore
{
    private const int RequestLifetimeMinutes = 10;
    private const int MaxDecisionHistory = 100;

    private readonly ConcurrentDictionary<Guid, DeviceLoginRequestState> _requests = new();
    private readonly ConcurrentQueue<LoginDecisionDto> _decisions = new();

    public DeviceLoginStartResponseDto CreateRequest(
        string requestedUserName,
        string deviceName,
        string? sourceIp,
        string? userAgent)
    {
        PruneExpired();

        var requestId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAtUtc = now.AddMinutes(RequestLifetimeMinutes);
        var userCode = BuildUserCode();

        var state = new DeviceLoginRequestState
        {
            RequestId = requestId,
            UserCode = userCode,
            RequestedUserName = requestedUserName,
            DeviceName = deviceName,
            SourceIp = sourceIp,
            UserAgent = userAgent,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            Status = DeviceLoginStatus.Pending
        };

        _requests[requestId] = state;

        return new DeviceLoginStartResponseDto(requestId, userCode, expiresAtUtc, PollIntervalSeconds: 3);
    }

    public IReadOnlyList<PendingLoginRequestDto> GetPendingRequests()
    {
        PruneExpired();

        return _requests.Values
            .Where(request => request.Status == DeviceLoginStatus.Pending)
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
    }

    public DeviceLoginPollSnapshot Poll(Guid requestId, string userCode)
    {
        PruneExpired();

        if (!_requests.TryGetValue(requestId, out var request))
        {
            return new DeviceLoginPollSnapshot("not_found", "Request not found.");
        }

        lock (request.SyncRoot)
        {
            if (!string.Equals(request.UserCode, userCode, StringComparison.OrdinalIgnoreCase))
            {
                return new DeviceLoginPollSnapshot("not_found", "Request not found.");
            }

            if (request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                request.Status = DeviceLoginStatus.Expired;
                return new DeviceLoginPollSnapshot("expired", "Request expired.");
            }

            if (request.Status == DeviceLoginStatus.Pending)
            {
                return new DeviceLoginPollSnapshot("pending", "Waiting for approval.");
            }

            if (request.Status == DeviceLoginStatus.Denied)
            {
                var reason = string.IsNullOrWhiteSpace(request.DenyReason) ? "Request denied." : request.DenyReason;
                return new DeviceLoginPollSnapshot("denied", reason);
            }

            if (request.Status != DeviceLoginStatus.Approved)
            {
                return new DeviceLoginPollSnapshot("error", "Unknown request state.");
            }

            if (!string.IsNullOrWhiteSpace(request.IssuedAccessToken) && request.IssuedAccessTokenExpiresAtUtc is not null)
            {
                return new DeviceLoginPollSnapshot(
                    "approved",
                    "Approved.",
                    DeviceName: request.DeviceName,
                    ExistingAccessToken: request.IssuedAccessToken,
                    ExistingAccessTokenExpiresAtUtc: request.IssuedAccessTokenExpiresAtUtc,
                    ExistingRefreshToken: request.IssuedRefreshToken,
                    ExistingRefreshTokenExpiresAtUtc: request.IssuedRefreshTokenExpiresAtUtc);
            }

            return new DeviceLoginPollSnapshot(
                "approved",
                "Approved.",
                RequestId: request.RequestId,
                DeviceName: request.DeviceName,
                UserName: request.ApprovedUserName,
                Roles: request.ApprovedRoles,
                TokenMinutes: request.ApprovedTokenMinutes);
        }
    }

    public bool Approve(Guid requestId, string userName, string roles, int? tokenMinutes)
    {
        PruneExpired();

        if (!_requests.TryGetValue(requestId, out var request))
        {
            return false;
        }

        lock (request.SyncRoot)
        {
            if (request.Status != DeviceLoginStatus.Pending || request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                return false;
            }

            request.Status = DeviceLoginStatus.Approved;
            request.ApprovedUserName = userName;
            request.ApprovedRoles = roles
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            request.ApprovedTokenMinutes = tokenMinutes;
            _decisions.Enqueue(new LoginDecisionDto(
                request.RequestId,
                request.DeviceName,
                "approved",
                userName,
                roles,
                null,
                DateTimeOffset.UtcNow));
            TrimDecisions();
            return true;
        }
    }

    public bool Deny(Guid requestId, string? reason)
    {
        PruneExpired();

        if (!_requests.TryGetValue(requestId, out var request))
        {
            return false;
        }

        lock (request.SyncRoot)
        {
            if (request.Status != DeviceLoginStatus.Pending)
            {
                return false;
            }

            request.Status = DeviceLoginStatus.Denied;
            request.DenyReason = reason;
            _decisions.Enqueue(new LoginDecisionDto(
                request.RequestId,
                request.DeviceName,
                "denied",
                null,
                null,
                reason,
                DateTimeOffset.UtcNow));
            TrimDecisions();
            return true;
        }
    }

    public void SaveIssuedToken(
        Guid requestId,
        string accessToken,
        DateTimeOffset accessTokenExpiresAtUtc,
        string refreshToken,
        DateTimeOffset? refreshTokenExpiresAtUtc)
    {
        if (!_requests.TryGetValue(requestId, out var request))
        {
            return;
        }

        lock (request.SyncRoot)
        {
            request.IssuedAccessToken = accessToken;
            request.IssuedAccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
            request.IssuedRefreshToken = refreshToken;
            request.IssuedRefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
        }
    }

    public IReadOnlyList<LoginDecisionDto> GetRecentDecisions(int maxCount = 25)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        return _decisions
            .Reverse()
            .Take(maxCount)
            .ToArray();
    }

    public void RecordLogoutEvent(string deviceName, string userName, string? roles)
    {
        _decisions.Enqueue(new LoginDecisionDto(
            Guid.NewGuid(),
            deviceName,
            "logout",
            userName,
            roles,
            "User logged out.",
            DateTimeOffset.UtcNow));
        TrimDecisions();
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var request in _requests)
        {
            if (request.Value.ExpiresAtUtc < now.AddMinutes(-5))
            {
                _requests.TryRemove(request.Key, out _);
            }
        }
    }

    private static string BuildUserCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;

        var left = new char[3];
        var right = new char[3];

        for (var i = 0; i < 3; i++)
        {
            left[i] = chars[random.Next(chars.Length)];
            right[i] = chars[random.Next(chars.Length)];
        }

        return $"{new string(left)}-{new string(right)}";
    }

    private void TrimDecisions()
    {
        while (_decisions.Count > MaxDecisionHistory)
        {
            _decisions.TryDequeue(out _);
        }
    }

    private sealed class DeviceLoginRequestState
    {
        public Guid RequestId { get; set; }

        public string UserCode { get; set; } = string.Empty;

        public string DeviceName { get; set; } = string.Empty;

        public string RequestedUserName { get; set; } = string.Empty;

        public string? SourceIp { get; set; }

        public string? UserAgent { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset ExpiresAtUtc { get; set; }

        public DeviceLoginStatus Status { get; set; }

        public string? ApprovedUserName { get; set; }

        public string[] ApprovedRoles { get; set; } = [];

        public int? ApprovedTokenMinutes { get; set; }

        public string? DenyReason { get; set; }

        public string? IssuedAccessToken { get; set; }

        public DateTimeOffset? IssuedAccessTokenExpiresAtUtc { get; set; }

        public string? IssuedRefreshToken { get; set; }

        public DateTimeOffset? IssuedRefreshTokenExpiresAtUtc { get; set; }

        public object SyncRoot { get; } = new();
    }

    private enum DeviceLoginStatus
    {
        Pending,
        Approved,
        Denied,
        Expired
    }
}
