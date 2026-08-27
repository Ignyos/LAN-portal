using Ignyos.LanPortal.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public sealed class SqliteAccessRequestStore(
    IOptions<BootstrapOptions> bootstrapOptions,
    ILogger<SqliteAccessRequestStore> logger) : IAccessRequestStore
{
    private const string TableName = "AccessRequests";

    public AccessRequestRecord Create(
        Guid requestId,
        string requestedUserName,
        string deviceName,
        string? sourceIp,
        string? userAgent,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        string userCode,
        AccessHistoryRecord history)
    {
        var record = new AccessRequestRecord(
            requestId, userCode, requestedUserName, deviceName, sourceIp, userAgent,
            createdAtUtc, expiresAtUtc, AccessRequestStatus.Pending, null, null, null, null, null,
            null, null, null, null);

        var created = Execute(connection =>
        {
            EnsureSchema(connection);
            EnsureHistorySchema(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
INSERT INTO {TableName} (
    RequestId, UserCode, RequestedUserName, DeviceName, SourceIp, UserAgent,
    CreatedAtUtc, ExpiresAtUtc, Status, DecidedAtUtc, DecisionReason,
    ApprovedUserName, ApprovedRoles, ApprovedTokenMinutes,
    IssuedAccessToken, IssuedAccessTokenExpiresAtUtc, IssuedRefreshToken, IssuedRefreshTokenExpiresAtUtc)
VALUES ($requestId, $userCode, $requestedUserName, $deviceName, $sourceIp, $userAgent,
    $createdAtUtc, $expiresAtUtc, $status, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
""";
            AddParameters(command, record);
            command.ExecuteNonQuery();
            InsertHistory(connection, transaction, history);
            transaction.Commit();
            return true;
        });

        if (!created)
        {
            throw new InvalidOperationException("Unable to create the access request.");
        }

        return record;
    }

    public AccessRequestRecord? Get(Guid requestId, string userCode)
        => QuerySingle(
            "WHERE RequestId = $requestId AND UserCode = $userCode",
            command =>
            {
                command.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
                command.Parameters.AddWithValue("$userCode", userCode);
            });

    public IReadOnlyList<AccessRequestRecord> GetPending()
        => QueryMany("WHERE Status = $status ORDER BY CreatedAtUtc", command =>
            command.Parameters.AddWithValue("$status", AccessRequestStatus.Pending.ToString()));

    public bool Approve(Guid requestId, string userName, string roles, int? tokenMinutes, string deviceName, DateTimeOffset decidedAtUtc, AccessHistoryRecord history)
        => ExecuteTransitionWithHistory(requestId, $"""
UPDATE {TableName}
SET Status = $approved, ApprovedUserName = $userName, ApprovedRoles = $roles,
    ApprovedTokenMinutes = $tokenMinutes, DeviceName = $deviceName,
    DecidedAtUtc = $decidedAtUtc, DecisionReason = NULL
WHERE RequestId = $requestId AND Status = $pending AND ExpiresAtUtc > $now;
""", command =>
        {
            command.Parameters.AddWithValue("$approved", AccessRequestStatus.Approved.ToString());
            command.Parameters.AddWithValue("$userName", userName);
            command.Parameters.AddWithValue("$roles", roles);
            command.Parameters.AddWithValue("$tokenMinutes", tokenMinutes ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$deviceName", deviceName);
            command.Parameters.AddWithValue("$decidedAtUtc", decidedAtUtc.ToString("O"));
        }, history);

    public bool Deny(Guid requestId, string? reason, DateTimeOffset decidedAtUtc, AccessHistoryRecord history)
        => ExecuteTransitionWithHistory(requestId, $"""
UPDATE {TableName}
SET Status = $denied, DecisionReason = $reason, DecidedAtUtc = $decidedAtUtc
WHERE RequestId = $requestId AND Status = $pending AND ExpiresAtUtc > $now;
""", command =>
        {
            command.Parameters.AddWithValue("$denied", AccessRequestStatus.Denied.ToString());
            command.Parameters.AddWithValue("$reason", reason ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$decidedAtUtc", decidedAtUtc.ToString("O"));
        }, history);

    public bool MarkExpired(Guid requestId, DateTimeOffset expiredAtUtc, AccessHistoryRecord history)
        => ExecuteTransitionWithHistory(requestId, $"""
UPDATE {TableName}
SET Status = $expired, DecisionReason = 'Access request expired.', DecidedAtUtc = $recordedAtUtc
WHERE RequestId = $requestId AND Status = $pending AND ExpiresAtUtc <= $now;
""", command =>
        {
            command.Parameters.AddWithValue("$expired", AccessRequestStatus.Expired.ToString());
            command.Parameters.AddWithValue("$recordedAtUtc", expiredAtUtc.ToString("O"));
        }, history);

    public IReadOnlyList<AccessRequestRecord> GetPendingExpired(DateTimeOffset nowUtc, int maxCount = 1000)
        => QueryMany("WHERE Status = $status AND ExpiresAtUtc <= $now ORDER BY ExpiresAtUtc LIMIT $maxCount", command =>
        {
            command.Parameters.AddWithValue("$status", AccessRequestStatus.Pending.ToString());
            command.Parameters.AddWithValue("$now", nowUtc.ToString("O"));
            command.Parameters.AddWithValue("$maxCount", maxCount);
        });

    public bool SaveIssuedToken(Guid requestId, string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset? refreshTokenExpiresAtUtc)
        => ExecuteTransition(requestId, $"""
UPDATE {TableName}
SET IssuedAccessToken = $accessToken,
    IssuedAccessTokenExpiresAtUtc = $accessTokenExpiresAtUtc,
    IssuedRefreshToken = $refreshToken,
    IssuedRefreshTokenExpiresAtUtc = $refreshTokenExpiresAtUtc
WHERE RequestId = $requestId;
""", command =>
        {
            command.Parameters.AddWithValue("$accessToken", accessToken);
            command.Parameters.AddWithValue("$accessTokenExpiresAtUtc", accessTokenExpiresAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$refreshToken", refreshToken);
            command.Parameters.AddWithValue("$refreshTokenExpiresAtUtc", refreshTokenExpiresAtUtc?.ToString("O") ?? (object)DBNull.Value);
        });

    public int PurgeCompletedBefore(DateTimeOffset cutoffUtc)
        => Execute(connection =>
        {
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {TableName} WHERE Status <> $pending AND DecidedAtUtc < $cutoff;";
            command.Parameters.AddWithValue("$pending", AccessRequestStatus.Pending.ToString());
            command.Parameters.AddWithValue("$cutoff", cutoffUtc.ToString("O"));
            return command.ExecuteNonQuery();
        });

    private bool ExecuteTransition(Guid requestId, string sql, Action<SqliteCommand> addParameters)
        => Execute(connection =>
        {
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
            command.Parameters.AddWithValue("$pending", AccessRequestStatus.Pending.ToString());
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            addParameters(command);
            return command.ExecuteNonQuery() > 0;
        });

    private bool ExecuteTransitionWithHistory(Guid requestId, string sql, Action<SqliteCommand> addParameters, AccessHistoryRecord history)
        => Execute(connection =>
        {
            EnsureSchema(connection);
            EnsureHistorySchema(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
            command.Parameters.AddWithValue("$pending", AccessRequestStatus.Pending.ToString());
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            addParameters(command);
            if (command.ExecuteNonQuery() == 0)
            {
                transaction.Rollback();
                return false;
            }

            InsertHistory(connection, transaction, history);
            transaction.Commit();
            return true;
        });

    private AccessRequestRecord? QuerySingle(string where, Action<SqliteCommand> addParameters)
        => QueryMany(where, addParameters).FirstOrDefault();

    private IReadOnlyList<AccessRequestRecord> QueryMany(string where, Action<SqliteCommand> addParameters)
        => Execute(connection =>
        {
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT RequestId, UserCode, RequestedUserName, DeviceName, SourceIp, UserAgent, CreatedAtUtc, ExpiresAtUtc, Status, DecidedAtUtc, DecisionReason, ApprovedUserName, ApprovedRoles, ApprovedTokenMinutes, IssuedAccessToken, IssuedAccessTokenExpiresAtUtc, IssuedRefreshToken, IssuedRefreshTokenExpiresAtUtc FROM {TableName} {where};";
            addParameters(command);
            using var reader = command.ExecuteReader();
            var records = new List<AccessRequestRecord>();
            while (reader.Read()) records.Add(ReadRecord(reader));
            return (IReadOnlyList<AccessRequestRecord>)records;
        });

    private T Execute<T>(Func<SqliteConnection, T> action)
    {
        try
        {
            using var connection = CreateOpenConnection();
            return action(connection);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Access request store operation failed.");
            return default!;
        }
    }

    private void Execute(Action<SqliteConnection> action) => Execute<object>(connection => { action(connection); return null!; });

    private SqliteConnection CreateOpenConnection()
    {
        var configured = bootstrapOptions.Value.DatabasePath;
        if (string.IsNullOrWhiteSpace(configured)) configured = "data/lanportal.db";
        var databasePath = Path.IsPathRooted(configured) ? configured : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
CREATE TABLE IF NOT EXISTS {TableName} (
    RequestId TEXT NOT NULL PRIMARY KEY,
    UserCode TEXT NOT NULL UNIQUE,
    RequestedUserName TEXT NOT NULL,
    DeviceName TEXT NOT NULL,
    SourceIp TEXT NULL,
    UserAgent TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    ExpiresAtUtc TEXT NOT NULL,
    Status TEXT NOT NULL,
    DecidedAtUtc TEXT NULL,
    DecisionReason TEXT NULL,
    ApprovedUserName TEXT NULL,
    ApprovedRoles TEXT NULL,
    ApprovedTokenMinutes INTEGER NULL,
    IssuedAccessToken TEXT NULL,
    IssuedAccessTokenExpiresAtUtc TEXT NULL,
    IssuedRefreshToken TEXT NULL,
    IssuedRefreshTokenExpiresAtUtc TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_{TableName}_StatusExpires ON {TableName}(Status, ExpiresAtUtc);
""";
        command.ExecuteNonQuery();
    }

    private static void EnsureHistorySchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE IF NOT EXISTS AccessHistory (
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
""";
        command.ExecuteNonQuery();
    }

    private static void InsertHistory(SqliteConnection connection, SqliteTransaction transaction, AccessHistoryRecord history)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO AccessHistory (
    HistoryId, EventKey, EventType, RequestId, SessionId, UserName,
    DeviceName, Roles, Reason, OccurredAtUtc, RecordedAtUtc)
VALUES (
    $historyId, $eventKey, $eventType, $requestId, $sessionId, $userName,
    $deviceName, $roles, $reason, $occurredAtUtc, $recordedAtUtc);
""";
        command.Parameters.AddWithValue("$historyId", history.HistoryId.ToString("D"));
        command.Parameters.AddWithValue("$eventKey", history.EventKey);
        command.Parameters.AddWithValue("$eventType", history.EventType);
        command.Parameters.AddWithValue("$requestId", history.RequestId?.ToString("D") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sessionId", history.SessionId?.ToString("D") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$userName", history.UserName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$deviceName", history.DeviceName);
        command.Parameters.AddWithValue("$roles", history.Roles ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$reason", history.Reason ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$occurredAtUtc", history.OccurredAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$recordedAtUtc", history.RecordedAtUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void AddParameters(SqliteCommand command, AccessRequestRecord record)
    {
        command.Parameters.AddWithValue("$requestId", record.RequestId.ToString("D"));
        command.Parameters.AddWithValue("$userCode", record.UserCode);
        command.Parameters.AddWithValue("$requestedUserName", record.RequestedUserName);
        command.Parameters.AddWithValue("$deviceName", record.DeviceName);
        command.Parameters.AddWithValue("$sourceIp", record.SourceIp ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$userAgent", record.UserAgent ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", record.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$expiresAtUtc", record.ExpiresAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$status", record.Status.ToString());
    }

    private static AccessRequestRecord ReadRecord(SqliteDataReader reader)
        => new(
            Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
            DateTimeOffset.Parse(reader.GetString(6)), DateTimeOffset.Parse(reader.GetString(7)),
            Enum.Parse<AccessRequestStatus>(reader.GetString(8)), reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)),
            reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetInt32(13),
            reader.IsDBNull(14) ? null : reader.GetString(14), reader.IsDBNull(15) ? null : DateTimeOffset.Parse(reader.GetString(15)),
            reader.IsDBNull(16) ? null : reader.GetString(16), reader.IsDBNull(17) ? null : DateTimeOffset.Parse(reader.GetString(17)));
}
