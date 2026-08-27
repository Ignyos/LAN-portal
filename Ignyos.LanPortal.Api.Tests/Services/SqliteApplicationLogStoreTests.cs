using Ignyos.LanPortal.Api;
using Ignyos.LanPortal.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ignyos.LanPortal.Api.Tests.Services;

public sealed class SqliteApplicationLogStoreTests
{
    [Fact]
    public void WritePersistsLogAndReadsRecent()
    {
        using var database = new TemporaryDatabase();
        var store = CreateStore(database);
        var logId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        store.Write(new ApplicationLogRecord(
            logId,
            occurredAt,
            ApplicationLogSeverity.Error,
            ApplicationLogCategory.App,
            "Program",
            "corr-123",
            "admin",
            "Desktop",
            "Startup failed while reading config.",
            "InvalidOperationException",
            "Key not found.",
            "{\"step\":\"bootstrap\"}",
            false));

        var logs = store.GetRecent();

        Assert.Single(logs);
        Assert.Equal(logId, logs[0].LogId);
        Assert.Equal(ApplicationLogSeverity.Error, logs[0].Severity);
        Assert.Equal(ApplicationLogCategory.App, logs[0].Category);
        Assert.Equal("Program", logs[0].Source);
        Assert.Equal("corr-123", logs[0].CorrelationId);
        Assert.Equal("admin", logs[0].UserName);
    }

    [Fact]
    public void GetRecentFiltersBySeverityAndCategoryAndPurgesOldEntries()
    {
        using var database = new TemporaryDatabase();
        var store = CreateStore(database);
        var now = DateTimeOffset.UtcNow;

        store.Write(new ApplicationLogRecord(Guid.NewGuid(), now.AddDays(-40), ApplicationLogSeverity.Error, ApplicationLogCategory.App, "Program", null, null, null, "Old", null, null, null, false));
        store.Write(new ApplicationLogRecord(Guid.NewGuid(), now.AddDays(-2), ApplicationLogSeverity.Warning, ApplicationLogCategory.App, "Program", null, null, null, "Recent warning", null, null, null, false));
        store.Write(new ApplicationLogRecord(Guid.NewGuid(), now.AddDays(-1), ApplicationLogSeverity.Error, ApplicationLogCategory.Admin, "Admin", null, null, null, "Recent error", null, null, null, false));
        store.Write(new ApplicationLogRecord(Guid.NewGuid(), now.AddDays(-1), ApplicationLogSeverity.Error, ApplicationLogCategory.Security, "Security", "corr-777", "host", "Host-PC", "JWT rotation succeeded.", null, null, null, false));

        var filtered = store.GetRecent(25, ApplicationLogSeverity.Error, ApplicationLogCategory.Security);
        var purged = store.PurgeBefore(now.AddDays(-7));

        Assert.Single(filtered);
        Assert.Equal(ApplicationLogSeverity.Error, filtered[0].Severity);
        Assert.Equal(ApplicationLogCategory.Security, filtered[0].Category);
        Assert.True(purged >= 1);
    }

    [Fact]
    public void ApplicationEventLoggerWritesAccessRequestEventToStore()
    {
        using var database = new TemporaryDatabase();
        var store = CreateStore(database);
        var logger = new ApplicationEventLogger(store);

        var requestId = Guid.NewGuid();
        logger.LogAccessRequestCreated(requestId, "Alice", "Laptop-1", "192.168.1.22");

        var logs = store.GetRecent(10);
        Assert.NotEmpty(logs);
        Assert.Contains(logs, item => item.Category == ApplicationLogCategory.DeviceAuth && item.Source == "InMemoryDeviceLoginStore" && item.Message.Contains("Access request created"));
        Assert.Contains(logs, item => item.CorrelationId == requestId.ToString("D"));
    }

    private static SqliteApplicationLogStore CreateStore(TemporaryDatabase database)
        => new(
            Options.Create(new BootstrapOptions { DatabasePath = database.Path }),
            NullLogger<SqliteApplicationLogStore>.Instance,
            new SqliteAppSettingsStore(Options.Create(new BootstrapOptions { DatabasePath = database.Path }), new DpapiValueProtector()));

    private sealed class TemporaryDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lanportal-app-log-{Guid.NewGuid():N}.db");

        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
            if (File.Exists($"{Path}-shm")) File.Delete($"{Path}-shm");
            if (File.Exists($"{Path}-wal")) File.Delete($"{Path}-wal");
        }
    }
}
