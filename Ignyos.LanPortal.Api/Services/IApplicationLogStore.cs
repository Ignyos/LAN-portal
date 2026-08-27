using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public enum ApplicationLogSeverity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public enum ApplicationLogCategory
{
    App,
    Host,
    DeviceAuth,
    Security,
    Admin,
    Maintenance
}

public sealed record ApplicationLogRecord(
    Guid LogId,
    DateTimeOffset OccurredAtUtc,
    ApplicationLogSeverity Severity,
    ApplicationLogCategory Category,
    string Source,
    string? CorrelationId,
    string? UserName,
    string? DeviceName,
    string Message,
    string? ExceptionType,
    string? ExceptionMessage,
    string? DetailsJson,
    bool IsRedacted = false);

public interface IApplicationLogStore
{
    void Write(ApplicationLogRecord record);

    IReadOnlyList<ApplicationLogRecord> GetRecent(
        int maxCount = 100,
        ApplicationLogSeverity? minimumSeverity = null,
        ApplicationLogCategory? category = null);

    int PurgeBefore(DateTimeOffset cutoffUtc);
}

public sealed class SqliteApplicationLogStore(
    IOptions<BootstrapOptions> bootstrapOptions,
    ILogger<SqliteApplicationLogStore> logger,
    IAppSettingsStore? settingsStore = null) : IApplicationLogStore
{
    private const string TableName = "ApplicationLogs";
    private readonly IAppSettingsStore? _settingsStore = settingsStore;

    public void Write(ApplicationLogRecord record)
    {
        try
        {
            var sanitized = Sanitize(record);
            using var connection = CreateOpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"""
INSERT INTO {TableName} (
    LogId, OccurredAtUtc, Severity, Category, Source, CorrelationId,
    UserName, DeviceName, Message, ExceptionType, ExceptionMessage,
    DetailsJson, IsRedacted)
VALUES (
    $logId, $occurredAtUtc, $severity, $category, $source, $correlationId,
    $userName, $deviceName, $message, $exceptionType, $exceptionMessage,
    $detailsJson, $isRedacted);
""";
            command.Parameters.AddWithValue("$logId", sanitized.LogId.ToString("D"));
            command.Parameters.AddWithValue("$occurredAtUtc", sanitized.OccurredAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$severity", sanitized.Severity.ToString());
            command.Parameters.AddWithValue("$category", sanitized.Category.ToString());
            command.Parameters.AddWithValue("$source", sanitized.Source);
            command.Parameters.AddWithValue("$correlationId", sanitized.CorrelationId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$userName", sanitized.UserName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$deviceName", sanitized.DeviceName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$message", sanitized.Message);
            command.Parameters.AddWithValue("$exceptionType", sanitized.ExceptionType ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$exceptionMessage", sanitized.ExceptionMessage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$detailsJson", sanitized.DetailsJson ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$isRedacted", sanitized.IsRedacted ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to write application log entry for source {Source}.", record.Source);
        }
    }

    public IReadOnlyList<ApplicationLogRecord> GetRecent(
        int maxCount = 100,
        ApplicationLogSeverity? minimumSeverity = null,
        ApplicationLogCategory? category = null)
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
            var conditions = new List<string>();

            if (minimumSeverity is not null)
            {
                var allowedLevels = Enum.GetValues<ApplicationLogSeverity>()
                    .Where(level => (int)level >= (int)minimumSeverity.Value)
                    .ToList();

                if (allowedLevels.Count > 0)
                {
                    var severityParameters = string.Join(", ", allowedLevels.Select(level => $"$severity{(int)level}"));
                    conditions.Add($"Severity IN ({severityParameters})");

                    foreach (var level in allowedLevels)
                    {
                        command.Parameters.AddWithValue($"$severity{(int)level}", level.ToString());
                    }
                }
            }

            if (category is not null)
            {
                conditions.Add("Category = $category");
                command.Parameters.AddWithValue("$category", category.Value.ToString());
            }

            var whereClause = conditions.Count > 0
                ? " WHERE " + string.Join(" AND ", conditions)
                : string.Empty;

            command.CommandText = $"""
SELECT LogId, OccurredAtUtc, Severity, Category, Source, CorrelationId,
       UserName, DeviceName, Message, ExceptionType, ExceptionMessage,
       DetailsJson, IsRedacted
FROM {TableName}{whereClause}
ORDER BY OccurredAtUtc DESC, LogId DESC
LIMIT $maxCount;
""";

            command.Parameters.AddWithValue("$maxCount", maxCount);

            using var reader = command.ExecuteReader();
            var records = new List<ApplicationLogRecord>();
            while (reader.Read())
            {
                records.Add(ReadRecord(reader));
            }

            return records;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to read recent application logs.");
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
            logger.LogError(exception, "Unable to purge application logs before {CutoffUtc}.", cutoffUtc);
            return 0;
        }
    }

    public int GetRetentionDays() => _settingsStore?.GetApplicationLogRetentionDays() ?? 30;

    private static ApplicationLogRecord Sanitize(ApplicationLogRecord record)
    {
        var message = Redact(record.Message) ?? string.Empty;
        var exceptionMessage = Redact(record.ExceptionMessage);
        var detailsJson = Redact(record.DetailsJson);
        var source = Redact(record.Source) ?? string.Empty;
        var correlationId = Redact(record.CorrelationId);
        var userName = Redact(record.UserName);
        var deviceName = Redact(record.DeviceName);

        return record with
        {
            Message = message,
            ExceptionMessage = exceptionMessage,
            DetailsJson = detailsJson,
            Source = source,
            CorrelationId = correlationId,
            UserName = userName,
            DeviceName = deviceName
        };
    }

    private static string? Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var redacted = value;
        redacted = System.Text.RegularExpressions.Regex.Replace(redacted, @"(?i)(Bearer\s+)([A-Za-z0-9._\-~+/]+=*)", "$1[REDACTED]");
        redacted = System.Text.RegularExpressions.Regex.Replace(redacted, @"(?i)(token|refreshToken|accessToken|jwt|signingKey|password|secret|authorization)\s*[:=]\s*([^,\s;\]]+)", "$1=[REDACTED]");
        return redacted;
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
    LogId TEXT NOT NULL PRIMARY KEY,
    OccurredAtUtc TEXT NOT NULL,
    Severity TEXT NOT NULL,
    Category TEXT NOT NULL,
    Source TEXT NOT NULL,
    CorrelationId TEXT NULL,
    UserName TEXT NULL,
    DeviceName TEXT NULL,
    Message TEXT NOT NULL,
    ExceptionType TEXT NULL,
    ExceptionMessage TEXT NULL,
    DetailsJson TEXT NULL,
    IsRedacted INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_{TableName}_OccurredAtUtc ON {TableName}(OccurredAtUtc DESC);
CREATE INDEX IF NOT EXISTS IX_{TableName}_Category ON {TableName}(Category);
CREATE INDEX IF NOT EXISTS IX_{TableName}_Severity ON {TableName}(Severity);
""";
        command.ExecuteNonQuery();
    }

    private static ApplicationLogRecord ReadRecord(SqliteDataReader reader)
        => new(
            Guid.Parse(reader.GetString(0)),
            DateTimeOffset.Parse(reader.GetString(1)),
            Enum.Parse<ApplicationLogSeverity>(reader.GetString(2)),
            Enum.Parse<ApplicationLogCategory>(reader.GetString(3)),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetInt32(12) != 0);
}
