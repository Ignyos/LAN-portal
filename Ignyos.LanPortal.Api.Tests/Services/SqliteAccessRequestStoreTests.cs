using Ignyos.LanPortal.Api;
using Ignyos.LanPortal.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ignyos.LanPortal.Api.Tests.Services;

public sealed class SqliteAccessRequestStoreTests
{
    [Fact]
    public void CreatePersistsRequestAndHistoryAtomically()
    {
        using var database = new TemporaryDatabase();
        var store = CreateStore(database);
        var requestId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var history = CreateHistory(requestId, "requested", now);

        var request = store.Create(requestId, "Alex", "Desktop", "192.168.1.20", "test-agent", now, now.AddMinutes(5), "ABC-123", history);

        Assert.Equal(requestId, request.RequestId);
        Assert.Equal(AccessRequestStatus.Pending, store.Get(requestId, request.UserCode)?.Status);
        Assert.Equal("AccessRequested", history.EventType);
    }

    [Fact]
    public void DuplicateHistoryKeyCannotCommitSecondApproval()
    {
        using var database = new TemporaryDatabase();
        var store = CreateStore(database);
        var requestId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = store.Create(requestId, "Alex", "Desktop", null, null, now, now.AddMinutes(5), "ABC-123", CreateHistory(requestId, "requested", now));
        var approvalHistory = CreateHistory(requestId, "approved", now.AddSeconds(1));

        Assert.True(store.Approve(requestId, "Alex", "User", 60, "Desktop", now.AddSeconds(1), approvalHistory));

        var secondRequestId = Guid.NewGuid();
        var secondRequest = store.Create(secondRequestId, "Sam", "Laptop", null, null, now, now.AddMinutes(5), "XYZ-789", CreateHistory(secondRequestId, "requested", now));
        Assert.False(store.Approve(secondRequestId, "Sam", "User", 60, "Laptop", now.AddSeconds(1), approvalHistory));
        Assert.Equal(AccessRequestStatus.Pending, store.Get(secondRequestId, secondRequest.UserCode)?.Status);
    }

    [Fact]
    public void ExpiredRequestCannotBeApproved()
    {
        using var database = new TemporaryDatabase();
        var store = CreateStore(database);
        var requestId = Guid.NewGuid();
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var request = store.Create(requestId, "Alex", "Desktop", null, null, expiredAt.AddMinutes(-5), expiredAt, "ABC-123", CreateHistory(requestId, "requested", expiredAt.AddMinutes(-5)));

        Assert.False(store.Approve(requestId, "Alex", "User", 60, "Desktop", DateTimeOffset.UtcNow, CreateHistory(requestId, "approved", DateTimeOffset.UtcNow)));
        Assert.True(store.MarkExpired(requestId, expiredAt, CreateHistory(requestId, "expired", expiredAt)));
        Assert.Equal(AccessRequestStatus.Expired, store.Get(requestId, request.UserCode)?.Status);
    }

    private static SqliteAccessRequestStore CreateStore(TemporaryDatabase database)
        => new(
            Options.Create(new BootstrapOptions { DatabasePath = database.Path }),
            NullLogger<SqliteAccessRequestStore>.Instance);

    private static AccessHistoryRecord CreateHistory(Guid requestId, string suffix, DateTimeOffset occurredAtUtc)
        => new(
            Guid.NewGuid(),
            $"request:{requestId}:{suffix}",
            suffix == "requested" ? "AccessRequested" : suffix == "approved" ? "AccessApproved" : suffix == "expired" ? "AccessRequestExpired" : "AccessRequested",
            requestId,
            null,
            "Alex",
            "Desktop",
            "User",
            null,
            occurredAtUtc,
            occurredAtUtc);

    private sealed class TemporaryDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lanportal-test-{Guid.NewGuid():N}.db");

        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
            if (File.Exists($"{Path}-shm")) File.Delete($"{Path}-shm");
            if (File.Exists($"{Path}-wal")) File.Delete($"{Path}-wal");
        }
    }
}
