using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public sealed class AccessHistoryMaintenanceService(
    IAppSettingsStore settingsStore,
    IAccessHistoryStore accessHistoryStore,
    IApplicationLogStore applicationLogStore,
    IAccessRequestStore accessRequestStore,
    ILogger<AccessHistoryMaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan InactiveSessionRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunMaintenanceAsync(stoppingToken);

        using var timer = new PeriodicTimer(MaintenanceInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunMaintenanceAsync(stoppingToken);
        }
    }

    private async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startedAt = Stopwatch.GetTimestamp();
            var now = DateTimeOffset.UtcNow;
            applicationLogStore.Write(new ApplicationLogRecord(
                Guid.NewGuid(),
                now,
                ApplicationLogSeverity.Information,
                ApplicationLogCategory.Maintenance,
                "AccessHistoryMaintenanceService",
                null,
                null,
                null,
                "Starting maintenance cycle.",
                null,
                null,
                null,
                false));

            var expiredSessions = settingsStore.GetExpiredAccessSessions();
            foreach (var session in expiredSessions)
            {
                accessHistoryStore.Record(new AccessHistoryRecord(
                    Guid.NewGuid(),
                    $"session:{session.SessionId}:expired",
                    AccessHistoryEventTypes.SessionExpired,
                    null,
                    session.SessionId,
                    session.UserName,
                    session.DeviceName,
                    session.Roles,
                    "Session expired.",
                    session.ExpiresAtUtc ?? now,
                    now));
            }

            var expiredRequests = accessRequestStore.GetPendingExpired(now);
            foreach (var request in expiredRequests)
            {
                accessRequestStore.MarkExpired(request.RequestId, request.ExpiresAtUtc, new AccessHistoryRecord(
                    Guid.NewGuid(),
                    $"request:{request.RequestId}:expired",
                    AccessHistoryEventTypes.AccessRequestExpired,
                    request.RequestId,
                    null,
                    request.RequestedUserName,
                    request.DeviceName,
                    null,
                    "Access request expired.",
                    request.ExpiresAtUtc,
                        now));
            }

            var sessionCutoff = now - InactiveSessionRetention;
            var purgedSessions = settingsStore.PurgeInactiveAccessSessions(sessionCutoff);
            var purgedRequests = accessRequestStore.PurgeCompletedBefore(sessionCutoff);
            var historyRetention = TimeSpan.FromDays(settingsStore.GetAccessHistoryRetentionDays());
            var purgedHistory = accessHistoryStore.PurgeBefore(now - historyRetention);
            var logRetention = TimeSpan.FromDays(settingsStore.GetApplicationLogRetentionDays());
            var purgedLogs = applicationLogStore.PurgeBefore(now - logRetention);
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var summaryMessage = $"Access maintenance completed in {elapsed.TotalMilliseconds:0.##} ms. Expiration events: {expiredSessions.Count + expiredRequests.Count}; purged sessions: {purgedSessions}; purged history: {purgedHistory}; purged logs: {purgedLogs}.";
            logger.LogInformation(summaryMessage);
            applicationLogStore.Write(new ApplicationLogRecord(
                Guid.NewGuid(),
                now,
                purgedLogs > 0 || expiredSessions.Count > 0 || expiredRequests.Count > 0
                    ? ApplicationLogSeverity.Warning
                    : ApplicationLogSeverity.Information,
                ApplicationLogCategory.Maintenance,
                "AccessHistoryMaintenanceService",
                null,
                null,
                null,
                summaryMessage,
                null,
                null,
                null,
                false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Access maintenance failed.");
        }

        await Task.CompletedTask;
    }
}

