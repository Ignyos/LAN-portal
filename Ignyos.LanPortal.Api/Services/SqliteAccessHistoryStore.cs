using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public sealed class SqliteAccessHistoryStore(
    IOptions<BootstrapOptions> bootstrapOptions,
    ILogger<SqliteAccessHistoryStore> logger) : IAccessHistoryStore
{
    private const string TableName = "AccessHistory";

    public bool Record(AccessHistoryRecord record)
    {
        try
        {
            using var connection = CreateOpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"""
INSERT OR IGNORE INTO {TableName} (
    HistoryId, EventKey, EventType, RequestId, SessionId, UserName,
    DeviceName, Roles, Reason, OccurredAtUtc, RecordedAtUtc)
VALUES (
    $historyId, $eventKey, $eventType, $requestId, $sessionId, $userName,
    $deviceName, $roles, $reason, $occurredAtUtc, $recordedAtUtc);
""";
            command.Parameters.AddWithValue("$historyId", record.HistoryId.ToString("D"));
            command.Parameters.AddWithValue("$eventKey", record.EventKey);
            command.Parameters.AddWithValue("$eventType", record.EventType);
            command.Parameters.AddWithValue("$requestId", record.RequestId?.ToString("D") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$sessionId", record.SessionId?.ToString("D") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$userName", record.UserName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$deviceName", record.DeviceName);
            command.Parameters.AddWithValue("$roles", record.Roles ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$reason", record.Reason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$occurredAtUtc", record.OccurredAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$recordedAtUtc", record.RecordedAtUtc.ToString("O"));
            return command.ExecuteNonQuery() > 0;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to record AccessHistory event {EventType}.", record.EventType);
            return false;
        }
    }

    public IReadOnlyList<AccessHistoryRecord> GetRecent(int maxCount = 100)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        try
        {
            using var connection = CreateOpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"""
SELECT HistoryId, EventKey, EventType, RequestId, SessionId, UserName,
       DeviceName, Roles, Reason, OccurredAtUtc, RecordedAtUtc
FROM {TableName}
ORDER BY OccurredAtUtc DESC, RecordedAtUtc DESC
LIMIT $maxCount;
""";
            command.Parameters.AddWithValue("$maxCount", maxCount);

            using var reader = command.ExecuteReader();
            var records = new List<AccessHistoryRecord>();
            while (reader.Read())
            {
                records.Add(ReadRecord(reader));
            }

            return records;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to read recent AccessHistory events.");
            return [];
        }
    }

    public int PurgeBefore(DateTimeOffset cutoffUtc)
    {
        try
        {
            using var connection = CreateOpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {TableName} WHERE OccurredAtUtc < $cutoffUtc;";
            command.Parameters.AddWithValue("$cutoffUtc", cutoffUtc.ToString("O"));
            return command.ExecuteNonQuery();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to purge AccessHistory before {CutoffUtc}.", cutoffUtc);
            return 0;
        }
    }

    private SqliteConnection CreateOpenConnection()
    {
        var configured = bootstrapOptions.Value.DatabasePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "data/lanportal.db";
        }

        var databasePath = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
CREATE TABLE IF NOT EXISTS {TableName} (
    HistoryId TEXT NOT NULL PRIMARY KEY,
    EventKey TEXT NOT NULL UNIQUE,
    EventType TEXT NOT NULL,
    RequestId TEXT NULL,
    SessionId TEXT NULL,
    UserName TEXT NULL,
    DeviceName TEXT NOT NULL,
    Roles TEXT NULL,
    Reason TEXT NULL,
    OccurredAtUtc TEXT NOT NULL,
    RecordedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_{TableName}_OccurredAtUtc ON {TableName}(OccurredAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_{TableName}_EventType ON {TableName}(EventType);
""";
        command.ExecuteNonQuery();
    }

    private static AccessHistoryRecord ReadRecord(SqliteDataReader reader)
        => new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
            reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            DateTimeOffset.Parse(reader.GetString(9)),
            DateTimeOffset.Parse(reader.GetString(10)));
}
