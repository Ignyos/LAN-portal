using Microsoft.Data.Sqlite;

namespace Ignyos.LanPortal.Api.Services;

public sealed class SessionLifecycleService(
    ISqliteConnectionFactory connectionFactory,
    ILogger<SessionLifecycleService> logger) : ISessionLifecycleService
{
    public AccessSessionRecord? Revoke(Guid sessionId, string reason)
        => ExecuteTransaction((connection, transaction) =>
        {
            var session = ReadSession(connection, transaction, "WHERE SessionId = $sessionId AND RevokedAtUtc IS NULL", command =>
                command.Parameters.AddWithValue("$sessionId", sessionId.ToString("D")));
            if (session is null) return null;

            var now = DateTimeOffset.UtcNow;
            UpdateRevoked(connection, transaction, sessionId, reason, now);
            InsertHistory(connection, transaction, session, AccessHistoryEventTypes.SessionRevoked, $"session:{sessionId}:revoked:{now.Ticks}", reason, now, now);
            transaction.Commit();
            return session;
        });

    public AccessSessionRecord? Logout(string jti, string reason)
        => ExecuteTransaction((connection, transaction) =>
        {
            var session = ReadSession(connection, transaction, "WHERE Jti = $jti AND RevokedAtUtc IS NULL", command =>
                command.Parameters.AddWithValue("$jti", jti));
            if (session is null) return null;

            var now = DateTimeOffset.UtcNow;
            UpdateRevoked(connection, transaction, session.SessionId, reason, now);
            InsertHistory(connection, transaction, session, AccessHistoryEventTypes.SessionLoggedOut, $"session:{session.SessionId}:logout", reason, now, now);
            transaction.Commit();
            return session;
        });

    public IReadOnlyList<AccessSessionRecord> RevokeByFilter(string? userName, string? deviceName, string reason)
        => ExecuteTransaction((connection, transaction) =>
        {
            var sessions = ReadSessions(connection, transaction, userName, deviceName);
            var now = DateTimeOffset.UtcNow;
            foreach (var session in sessions)
            {
                UpdateRevoked(connection, transaction, session.SessionId, reason, now);
                InsertHistory(connection, transaction, session, AccessHistoryEventTypes.SessionRevoked, $"session:{session.SessionId}:revoked:{now.Ticks}", reason, now, now);
            }

            transaction.Commit();
            return (IReadOnlyList<AccessSessionRecord>)sessions;
        }) ?? [];

    private T? ExecuteTransaction<T>(Func<SqliteConnection, SqliteTransaction, T?> action)
    {
        try
        {
            using var connection = connectionFactory.CreateOpenConnection();
            EnsureHistorySchema(connection);
            using var transaction = connection.BeginTransaction();
            return action(connection, transaction);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Session lifecycle transaction failed.");
            return default;
        }
    }

    private static AccessSessionRecord? ReadSession(SqliteConnection connection, SqliteTransaction transaction, string where, Action<SqliteCommand> parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT SessionId, Jti, UserName, DeviceName, Roles, IssuedAtUtc, ExpiresAtUtc, RevokedAtUtc, RevokedReason, LastSeenAtUtc FROM AccessSessions {where} LIMIT 1;";
        parameters(command);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSession(reader) : null;
    }

    private static List<AccessSessionRecord> ReadSessions(SqliteConnection connection, SqliteTransaction transaction, string? userName, string? deviceName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var filters = new List<string> { "RevokedAtUtc IS NULL", "(ExpiresAtUtc IS NULL OR ExpiresAtUtc > $now)" };
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        if (!string.IsNullOrWhiteSpace(userName)) { filters.Add("UserName = $userName"); command.Parameters.AddWithValue("$userName", userName.Trim()); }
        if (!string.IsNullOrWhiteSpace(deviceName)) { filters.Add("DeviceName = $deviceName"); command.Parameters.AddWithValue("$deviceName", deviceName.Trim()); }
        command.CommandText = $"SELECT SessionId, Jti, UserName, DeviceName, Roles, IssuedAtUtc, ExpiresAtUtc, RevokedAtUtc, RevokedReason, LastSeenAtUtc FROM AccessSessions WHERE {string.Join(" AND ", filters)};";
        using var reader = command.ExecuteReader();
        var sessions = new List<AccessSessionRecord>();
        while (reader.Read()) sessions.Add(ReadSession(reader));
        return sessions;
    }

    private static void UpdateRevoked(SqliteConnection connection, SqliteTransaction transaction, Guid sessionId, string reason, DateTimeOffset now)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE AccessSessions SET RevokedAtUtc = $now, RevokedReason = $reason WHERE SessionId = $sessionId AND RevokedAtUtc IS NULL;";
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        command.Parameters.AddWithValue("$reason", reason);
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Session was already changed.");
    }

    private static void InsertHistory(SqliteConnection connection, SqliteTransaction transaction, AccessSessionRecord session, string eventType, string eventKey, string reason, DateTimeOffset occurredAt, DateTimeOffset recordedAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO AccessHistory (HistoryId, EventKey, EventType, SessionId, UserName, DeviceName, Roles, Reason, OccurredAtUtc, RecordedAtUtc) VALUES ($id, $key, $type, $sessionId, $userName, $deviceName, $roles, $reason, $occurred, $recorded);";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$key", eventKey);
        command.Parameters.AddWithValue("$type", eventType);
        command.Parameters.AddWithValue("$sessionId", session.SessionId.ToString("D"));
        command.Parameters.AddWithValue("$userName", session.UserName);
        command.Parameters.AddWithValue("$deviceName", session.DeviceName);
        command.Parameters.AddWithValue("$roles", session.Roles);
        command.Parameters.AddWithValue("$reason", reason);
        command.Parameters.AddWithValue("$occurred", occurredAt.ToString("O"));
        command.Parameters.AddWithValue("$recorded", recordedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void EnsureHistorySchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS AccessHistory (HistoryId TEXT NOT NULL PRIMARY KEY, EventKey TEXT NOT NULL UNIQUE, EventType TEXT NOT NULL, RequestId TEXT NULL, SessionId TEXT NULL, UserName TEXT NULL, DeviceName TEXT NOT NULL, Roles TEXT NULL, Reason TEXT NULL, OccurredAtUtc TEXT NOT NULL, RecordedAtUtc TEXT NOT NULL);";
        command.ExecuteNonQuery();
    }

    private static AccessSessionRecord ReadSession(SqliteDataReader reader) => new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), DateTimeOffset.Parse(reader.GetString(5)), reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)), reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)), reader.IsDBNull(8) ? null : reader.GetString(8), DateTimeOffset.Parse(reader.GetString(9)));
}
