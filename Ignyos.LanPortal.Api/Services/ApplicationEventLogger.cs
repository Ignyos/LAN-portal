using System.Text.Json;

namespace Ignyos.LanPortal.Api.Services;

public sealed class ApplicationEventLogger(IApplicationLogStore applicationLogStore)
{
    public void LogAccessRequestCreated(Guid requestId, string requestedUserName, string deviceName, string? sourceIp)
        => Write(
            ApplicationLogSeverity.Information,
            ApplicationLogCategory.DeviceAuth,
            "InMemoryDeviceLoginStore",
            requestId.ToString("D"),
            requestedUserName,
            deviceName,
            $"Access request created for {requestedUserName} on device {deviceName}.",
            new { requestId, requestedUserName, deviceName, sourceIp });

    public void LogAccessRequestApproved(Guid requestId, string approvedUserName, string deviceName, string? roles)
        => Write(
            ApplicationLogSeverity.Information,
            ApplicationLogCategory.DeviceAuth,
            "InMemoryDeviceLoginStore",
            requestId.ToString("D"),
            approvedUserName,
            deviceName,
            $"Access request approved for {approvedUserName} on device {deviceName}.",
            new { requestId, approvedUserName, deviceName, roles });

    public void LogAccessRequestDenied(Guid requestId, string userName, string deviceName, string? reason)
        => Write(
            ApplicationLogSeverity.Warning,
            ApplicationLogCategory.DeviceAuth,
            "InMemoryDeviceLoginStore",
            requestId.ToString("D"),
            userName,
            deviceName,
            $"Access request denied for {userName} on device {deviceName}.",
            new { requestId, userName, deviceName, reason });

    public void LogAccessRequestExpired(Guid requestId, string userName, string deviceName)
        => Write(
            ApplicationLogSeverity.Warning,
            ApplicationLogCategory.DeviceAuth,
            "InMemoryDeviceLoginStore",
            requestId.ToString("D"),
            userName,
            deviceName,
            $"Access request expired for {userName} on device {deviceName}.",
            new { requestId, userName, deviceName });

    public void LogSessionRevoked(Guid sessionId, string userName, string deviceName, string reason)
        => Write(
            ApplicationLogSeverity.Warning,
            ApplicationLogCategory.Security,
            "SessionLifecycleService",
            sessionId.ToString("D"),
            userName,
            deviceName,
            $"Session revoked for {userName} on device {deviceName}.",
            new { sessionId, userName, deviceName, reason });

    public void LogSigningKeyRotated(int revokedSessionCount, string keyFingerprint)
        => Write(
            ApplicationLogSeverity.Warning,
            ApplicationLogCategory.Security,
            "LocalSecurityController",
            Guid.NewGuid().ToString("D"),
            null,
            null,
            $"JWT signing key rotated; {revokedSessionCount} active sessions invalidated.",
            new { revokedSessionCount, keyFingerprint });

    private void Write(
        ApplicationLogSeverity severity,
        ApplicationLogCategory category,
        string source,
        string correlationId,
        string? userName,
        string? deviceName,
        string message,
        object? details)
    {
        applicationLogStore.Write(new ApplicationLogRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            severity,
            category,
            source,
            correlationId,
            userName,
            deviceName,
            message,
            null,
            null,
            JsonSerializer.Serialize(details),
            false));
    }
}