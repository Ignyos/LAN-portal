using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public sealed class SqliteAppSettingsStore(
    IOptions<BootstrapOptions> bootstrapOptions,
    IValueProtector protector) : IAppSettingsStore
{
    private const string JwtIssuerKey = "Jwt:Issuer";
    private const string JwtAudienceKey = "Jwt:Audience";
    private const string JwtSigningKeyKey = "Jwt:SigningKey";
    private const string StorageRootPathKey = "Storage:RootPath";

    private readonly object _sync = new();
    private bool _initialized;

    public void Initialize()
    {
        lock (_sync)
        {
            if (_initialized)
            {
                return;
            }

            var databasePath = ResolveDatabasePath();
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT NOT NULL PRIMARY KEY,
    Value TEXT NOT NULL,
    IsSensitive INTEGER NOT NULL DEFAULT 0,
    UpdatedAtUtc TEXT NOT NULL
);
""";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
CREATE TABLE IF NOT EXISTS AccessSessions (
    SessionId TEXT NOT NULL PRIMARY KEY,
    Jti TEXT NOT NULL UNIQUE,
    UserName TEXT NOT NULL,
    DeviceName TEXT NOT NULL,
    Roles TEXT NOT NULL,
    IssuedAtUtc TEXT NOT NULL,
    ExpiresAtUtc TEXT NOT NULL,
    RevokedAtUtc TEXT NULL,
    RevokedReason TEXT NULL,
    LastSeenAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_AccessSessions_UserName ON AccessSessions(UserName);
CREATE INDEX IF NOT EXISTS IX_AccessSessions_DeviceName ON AccessSessions(DeviceName);
""";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
CREATE TABLE IF NOT EXISTS SessionRefreshTokens (
    SessionId TEXT NOT NULL PRIMARY KEY,
    RefreshTokenHash TEXT NOT NULL UNIQUE,
    ExpiresAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_SessionRefreshTokens_RefreshTokenHash ON SessionRefreshTokens(RefreshTokenHash);
""";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
CREATE TABLE IF NOT EXISTS RoleChangeAudits (
    AuditId TEXT NOT NULL PRIMARY KEY,
    SessionId TEXT NOT NULL,
    UserName TEXT NOT NULL,
    DeviceName TEXT NOT NULL,
    PreviousRoles TEXT NOT NULL,
    NewRoles TEXT NOT NULL,
    ChangedBy TEXT NOT NULL,
    Reason TEXT NOT NULL,
    ChangedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_RoleChangeAudits_ChangedAtUtc ON RoleChangeAudits(ChangedAtUtc DESC);
""";
                command.ExecuteNonQuery();
            }

            SeedIfMissing(connection, JwtIssuerKey, "Ignyos.LanPortal", isSensitive: false);
            SeedIfMissing(connection, JwtAudienceKey, "Ignyos.LanPortal.Clients", isSensitive: false);
            SeedIfMissing(connection, JwtSigningKeyKey, GenerateSigningKey(), isSensitive: true);
            SeedIfMissing(connection, StorageRootPathKey, string.Empty, isSensitive: false);

            _initialized = true;
        }
    }

    public JwtDatabaseConfig GetJwtConfig()
    {
        Initialize();

        var issuer = GetRequiredValue(JwtIssuerKey);
        var audience = GetRequiredValue(JwtAudienceKey);
        var signingKey = GetRequiredValue(JwtSigningKeyKey);

        return new JwtDatabaseConfig(issuer, audience, signingKey);
    }

    public string? GetStorageRootPath()
    {
        Initialize();
        var value = GetValue(StorageRootPathKey);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void SetStorageRootPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Storage root path is required.", nameof(rootPath));
        }

        Initialize();
        SetValue(StorageRootPathKey, rootPath.Trim(), isSensitive: false);
    }

    public bool IsSetupComplete()
    {
        return !string.IsNullOrWhiteSpace(GetStorageRootPath());
    }

    public void RecordIssuedAccessSession(AccessSessionRecord record)
    {
        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO AccessSessions (
    SessionId,
    Jti,
    UserName,
    DeviceName,
    Roles,
    IssuedAtUtc,
    ExpiresAtUtc,
    RevokedAtUtc,
    RevokedReason,
    LastSeenAtUtc)
VALUES (
    $sessionId,
    $jti,
    $userName,
    $deviceName,
    $roles,
    $issuedAtUtc,
    $expiresAtUtc,
    $revokedAtUtc,
    $revokedReason,
    $lastSeenAtUtc)
ON CONFLICT(SessionId) DO UPDATE SET
    Jti = excluded.Jti,
    UserName = excluded.UserName,
    DeviceName = excluded.DeviceName,
    Roles = excluded.Roles,
    IssuedAtUtc = excluded.IssuedAtUtc,
    ExpiresAtUtc = excluded.ExpiresAtUtc,
    RevokedAtUtc = excluded.RevokedAtUtc,
    RevokedReason = excluded.RevokedReason,
    LastSeenAtUtc = excluded.LastSeenAtUtc;
""";
        command.Parameters.AddWithValue("$sessionId", record.SessionId.ToString("D"));
        command.Parameters.AddWithValue("$jti", record.Jti);
        command.Parameters.AddWithValue("$userName", record.UserName);
        command.Parameters.AddWithValue("$deviceName", record.DeviceName);
        command.Parameters.AddWithValue("$roles", record.Roles);
        command.Parameters.AddWithValue("$issuedAtUtc", record.IssuedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$expiresAtUtc", record.ExpiresAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$revokedAtUtc", record.RevokedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$revokedReason", record.RevokedReason ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$lastSeenAtUtc", record.LastSeenAtUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void UpsertRefreshToken(Guid sessionId, string refreshTokenHash, DateTimeOffset refreshTokenExpiresAtUtc)
    {
        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO SessionRefreshTokens (
    SessionId,
    RefreshTokenHash,
    ExpiresAtUtc)
VALUES (
    $sessionId,
    $refreshTokenHash,
    $expiresAtUtc)
ON CONFLICT(SessionId) DO UPDATE SET
    RefreshTokenHash = excluded.RefreshTokenHash,
    ExpiresAtUtc = excluded.ExpiresAtUtc;
""";
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$refreshTokenHash", refreshTokenHash);
        command.Parameters.AddWithValue("$expiresAtUtc", refreshTokenExpiresAtUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    public AccessSessionRecord? GetActiveAccessSessionByRefreshTokenHash(string refreshTokenHash)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenHash))
        {
            return null;
        }

        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT s.SessionId, s.Jti, s.UserName, s.DeviceName, s.Roles, s.IssuedAtUtc, s.ExpiresAtUtc, s.RevokedAtUtc, s.RevokedReason, s.LastSeenAtUtc
FROM AccessSessions s
INNER JOIN SessionRefreshTokens r ON r.SessionId = s.SessionId
WHERE r.RefreshTokenHash = $refreshTokenHash
  AND r.ExpiresAtUtc > $now
  AND s.RevokedAtUtc IS NULL
  AND s.ExpiresAtUtc > $now
LIMIT 1;
""";
        command.Parameters.AddWithValue("$refreshTokenHash", refreshTokenHash);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return ReadSession(reader);
    }

    public bool RefreshAccessSession(Guid sessionId, string refreshTokenHash, string newJti, DateTimeOffset issuedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenHash) || string.IsNullOrWhiteSpace(newJti))
        {
            return false;
        }

        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE AccessSessions
SET Jti = $newJti,
    IssuedAtUtc = $issuedAtUtc,
    LastSeenAtUtc = $lastSeenAtUtc
WHERE SessionId = $sessionId
  AND RevokedAtUtc IS NULL
  AND ExpiresAtUtc > $now
  AND EXISTS (
      SELECT 1
      FROM SessionRefreshTokens r
      WHERE r.SessionId = AccessSessions.SessionId
        AND r.RefreshTokenHash = $refreshTokenHash
        AND r.ExpiresAtUtc > $now
  );
""";
        command.Parameters.AddWithValue("$newJti", newJti);
        command.Parameters.AddWithValue("$issuedAtUtc", issuedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$lastSeenAtUtc", issuedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$refreshTokenHash", refreshTokenHash);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

        return command.ExecuteNonQuery() > 0;
    }

    public bool UpdateAccessSessionRoles(Guid sessionId, string roles, string changedBy, string reason, DateTimeOffset changedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(roles) || string.IsNullOrWhiteSpace(changedBy) || string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        Initialize();

        using var connection = CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        string? userName = null;
        string? deviceName = null;
        string? previousRoles = null;

        using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText = """
SELECT UserName, DeviceName, Roles
FROM AccessSessions
WHERE SessionId = $sessionId
  AND RevokedAtUtc IS NULL
  AND ExpiresAtUtc > $now
LIMIT 1;
""";
            readCommand.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
            readCommand.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            using var reader = readCommand.ExecuteReader();
            if (!reader.Read())
            {
                transaction.Rollback();
                return false;
            }

            userName = reader.GetString(0);
            deviceName = reader.GetString(1);
            previousRoles = reader.GetString(2);
        }

        var normalizedPrevious = NormalizeRoles(previousRoles);
        var normalizedNew = NormalizeRoles(roles);
        if (string.Equals(normalizedPrevious, normalizedNew, StringComparison.OrdinalIgnoreCase))
        {
            transaction.Commit();
            return true;
        }

        var newJti = Guid.NewGuid().ToString("N");

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
UPDATE AccessSessions
SET Roles = $roles,
    Jti = $jti,
    LastSeenAtUtc = $lastSeenAtUtc
WHERE SessionId = $sessionId
  AND RevokedAtUtc IS NULL
  AND ExpiresAtUtc > $now;
""";
            updateCommand.Parameters.AddWithValue("$roles", normalizedNew);
            updateCommand.Parameters.AddWithValue("$jti", newJti);
            updateCommand.Parameters.AddWithValue("$lastSeenAtUtc", changedAtUtc.ToString("O"));
            updateCommand.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
            updateCommand.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            if (updateCommand.ExecuteNonQuery() == 0)
            {
                transaction.Rollback();
                return false;
            }
        }

        using (var auditCommand = connection.CreateCommand())
        {
            auditCommand.Transaction = transaction;
            auditCommand.CommandText = """
INSERT INTO RoleChangeAudits (
    AuditId,
    SessionId,
    UserName,
    DeviceName,
    PreviousRoles,
    NewRoles,
    ChangedBy,
    Reason,
    ChangedAtUtc)
VALUES (
    $auditId,
    $sessionId,
    $userName,
    $deviceName,
    $previousRoles,
    $newRoles,
    $changedBy,
    $reason,
    $changedAtUtc);
""";
            auditCommand.Parameters.AddWithValue("$auditId", Guid.NewGuid().ToString("D"));
            auditCommand.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
            auditCommand.Parameters.AddWithValue("$userName", userName ?? string.Empty);
            auditCommand.Parameters.AddWithValue("$deviceName", deviceName ?? string.Empty);
            auditCommand.Parameters.AddWithValue("$previousRoles", normalizedPrevious);
            auditCommand.Parameters.AddWithValue("$newRoles", normalizedNew);
            auditCommand.Parameters.AddWithValue("$changedBy", changedBy.Trim());
            auditCommand.Parameters.AddWithValue("$reason", reason.Trim());
            auditCommand.Parameters.AddWithValue("$changedAtUtc", changedAtUtc.ToString("O"));
            auditCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return true;
    }

    public IReadOnlyList<RoleChangeAuditRecord> GetRecentRoleChanges(int maxCount = 100)
    {
        Initialize();

        if (maxCount <= 0)
        {
            return [];
        }

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT AuditId, SessionId, UserName, DeviceName, PreviousRoles, NewRoles, ChangedBy, Reason, ChangedAtUtc
FROM RoleChangeAudits
ORDER BY ChangedAtUtc DESC
LIMIT $maxCount;
""";
        command.Parameters.AddWithValue("$maxCount", maxCount);

        using var reader = command.ExecuteReader();
        var items = new List<RoleChangeAuditRecord>();
        while (reader.Read())
        {
            items.Add(new RoleChangeAuditRecord(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                DateTimeOffset.Parse(reader.GetString(8))));
        }

        return items;
    }

    public bool IsAccessTokenActive(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT SessionId, UserName, DeviceName, Roles, IssuedAtUtc, ExpiresAtUtc, RevokedAtUtc, RevokedReason, LastSeenAtUtc
FROM AccessSessions
WHERE Jti = $jti;
""";
        command.Parameters.AddWithValue("$jti", jti);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        var expiresAtUtc = DateTimeOffset.Parse(reader.GetString(5));
        var revokedAtUtc = reader.IsDBNull(6) ? (DateTimeOffset?)null : DateTimeOffset.Parse(reader.GetString(6));

        if (revokedAtUtc is not null || expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        reader.Close();

        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE AccessSessions SET LastSeenAtUtc = $lastSeenAtUtc WHERE Jti = $jti";
        update.Parameters.AddWithValue("$lastSeenAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$jti", jti);
        update.ExecuteNonQuery();

        return true;
    }

    public IReadOnlyList<AccessSessionRecord> GetActiveAccessSessions(int maxCount = 250)
    {
        Initialize();

        if (maxCount <= 0)
        {
            return [];
        }

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT SessionId, Jti, UserName, DeviceName, Roles, IssuedAtUtc, ExpiresAtUtc, RevokedAtUtc, RevokedReason, LastSeenAtUtc
FROM AccessSessions
WHERE RevokedAtUtc IS NULL
  AND ExpiresAtUtc > $now
ORDER BY IssuedAtUtc DESC
LIMIT $maxCount;
""";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$maxCount", maxCount);

        using var reader = command.ExecuteReader();
        var sessions = new List<AccessSessionRecord>();
        while (reader.Read())
        {
            sessions.Add(ReadSession(reader));
        }

        return sessions;
    }

    public bool RevokeAccessSession(Guid sessionId, string reason)
    {
        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE AccessSessions
SET RevokedAtUtc = $revokedAtUtc,
    RevokedReason = $reason
WHERE SessionId = $sessionId
  AND RevokedAtUtc IS NULL;
""";
        command.Parameters.AddWithValue("$revokedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$reason", reason);
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
        var rows = command.ExecuteNonQuery();
        return rows > 0;
    }

    public AccessSessionRecord? RevokeAccessSessionByJti(string jti, string reason)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return null;
        }

        Initialize();

        using var connection = CreateOpenConnection();

        AccessSessionRecord? session = null;
        using (var read = connection.CreateCommand())
        {
            read.CommandText = """
SELECT SessionId, Jti, UserName, DeviceName, Roles, IssuedAtUtc, ExpiresAtUtc, RevokedAtUtc, RevokedReason, LastSeenAtUtc
FROM AccessSessions
WHERE Jti = $jti
  AND RevokedAtUtc IS NULL
LIMIT 1;
""";
            read.Parameters.AddWithValue("$jti", jti);
            using var reader = read.ExecuteReader();
            if (reader.Read())
            {
                session = ReadSession(reader);
            }
        }

        if (session is null)
        {
            return null;
        }

        using var update = connection.CreateCommand();
        update.CommandText = """
UPDATE AccessSessions
SET RevokedAtUtc = $revokedAtUtc,
    RevokedReason = $reason
WHERE SessionId = $sessionId
  AND RevokedAtUtc IS NULL;
""";
        update.Parameters.AddWithValue("$revokedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$reason", reason);
        update.Parameters.AddWithValue("$sessionId", session.SessionId.ToString("D"));
        return update.ExecuteNonQuery() > 0 ? session : null;
    }

    public int RevokeAccessByUserDevice(string? userName, string? deviceName, string reason)
    {
        Initialize();

        if (string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(deviceName))
        {
            return 0;
        }

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        var where = new List<string> { "RevokedAtUtc IS NULL", "ExpiresAtUtc > $now" };
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$revokedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$reason", reason);

        if (!string.IsNullOrWhiteSpace(userName))
        {
            where.Add("UserName = $userName");
            command.Parameters.AddWithValue("$userName", userName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            where.Add("DeviceName = $deviceName");
            command.Parameters.AddWithValue("$deviceName", deviceName.Trim());
        }

        command.CommandText = $"""
UPDATE AccessSessions
SET RevokedAtUtc = $revokedAtUtc,
    RevokedReason = $reason
WHERE {string.Join(" AND ", where)};
""";

        return command.ExecuteNonQuery();
    }

    private string GetRequiredValue(string key)
    {
        var value = GetValue(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required setting '{key}' is missing.");
        }

        return value;
    }

    private string? GetValue(string key)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value, IsSensitive FROM AppSettings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var storedValue = reader.GetString(0);
        var isSensitive = reader.GetInt32(1) == 1;

        return isSensitive ? protector.Unprotect(storedValue) : storedValue;
    }

    private void SetValue(string key, string value, bool isSensitive)
    {
        using var connection = CreateOpenConnection();
        var toStore = isSensitive ? protector.Protect(value) : value;

        using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO AppSettings (Key, Value, IsSensitive, UpdatedAtUtc)
VALUES ($key, $value, $isSensitive, $updatedAtUtc)
ON CONFLICT(Key) DO UPDATE SET
    Value = excluded.Value,
    IsSensitive = excluded.IsSensitive,
    UpdatedAtUtc = excluded.UpdatedAtUtc;
""";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", toStore);
        command.Parameters.AddWithValue("$isSensitive", isSensitive ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void SeedIfMissing(SqliteConnection connection, string key, string value, bool isSensitive)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(1) FROM AppSettings WHERE Key = $key";
        check.Parameters.AddWithValue("$key", key);

        var count = Convert.ToInt32(check.ExecuteScalar());
        if (count > 0)
        {
            return;
        }

        var toStore = isSensitive ? protector.Protect(value) : value;

        using var insert = connection.CreateCommand();
        insert.CommandText = """
INSERT INTO AppSettings (Key, Value, IsSensitive, UpdatedAtUtc)
VALUES ($key, $value, $isSensitive, $updatedAtUtc);
""";
        insert.Parameters.AddWithValue("$key", key);
        insert.Parameters.AddWithValue("$value", toStore);
        insert.Parameters.AddWithValue("$isSensitive", isSensitive ? 1 : 0);
        insert.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    private SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={ResolveDatabasePath()}");
        connection.Open();
        return connection;
    }

    private string ResolveDatabasePath()
    {
        var configured = bootstrapOptions.Value.DatabasePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "data/lanportal.db";
        }

        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        var resolvedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        if (IsUnderProgramFiles(resolvedPath))
        {
            var machineDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Ignyos",
                "LanPortal");
            var relativeConfigured = configured
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            return Path.Combine(machineDataRoot, relativeConfigured);
        }

        return resolvedPath;
    }

    private static bool IsUnderProgramFiles(string path)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var programFilesPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);

        return programFilesPaths.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateSigningKey()
    {
        Span<byte> keyBytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(keyBytes);
        return Convert.ToBase64String(keyBytes);
    }

    private static string NormalizeRoles(string roles)
    {
        var cleaned = roles
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(role => role.Trim())
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length == 0 ? "User" : string.Join(',', cleaned);
    }

    private static AccessSessionRecord ReadSession(SqliteDataReader reader)
    {
        return new AccessSessionRecord(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5)),
            DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            DateTimeOffset.Parse(reader.GetString(9)));
    }
}
