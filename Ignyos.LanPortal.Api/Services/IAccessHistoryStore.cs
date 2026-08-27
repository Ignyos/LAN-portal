namespace Ignyos.LanPortal.Api.Services;

public static class AccessHistoryEventTypes
{
    public const string AccessRequested = "AccessRequested";
    public const string AccessApproved = "AccessApproved";
    public const string AccessDenied = "AccessDenied";
    public const string AccessRequestExpired = "AccessRequestExpired";
    public const string SessionRevoked = "SessionRevoked";
    public const string SessionLoggedOut = "SessionLoggedOut";
    public const string SessionExpired = "SessionExpired";
}

public sealed record AccessHistoryRecord(
    Guid HistoryId,
    string EventKey,
    string EventType,
    Guid? RequestId,
    Guid? SessionId,
    string? UserName,
    string DeviceName,
    string? Roles,
    string? Reason,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset RecordedAtUtc);

public interface IAccessHistoryStore
{
    bool Record(AccessHistoryRecord record);

    IReadOnlyList<AccessHistoryRecord> GetRecent(int maxCount = 100);

    int PurgeBefore(DateTimeOffset cutoffUtc);
}
