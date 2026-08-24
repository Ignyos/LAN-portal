using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public sealed class SqliteHostUiStateStore(
    IOptions<BootstrapOptions> bootstrapOptions,
    ILogger<SqliteHostUiStateStore> logger) : IHostUiStateStore
{
    private const string TableName = "HostUiState";

    public IReadOnlyDictionary<string, bool> GetPageState(string pageKey)
    {
        if (string.IsNullOrWhiteSpace(pageKey))
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var connection = CreateOpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT SectionKey, IsExpanded FROM {TableName} WHERE PageKey = $pageKey;";
            command.Parameters.AddWithValue("$pageKey", pageKey.Trim());

            using var reader = command.ExecuteReader();
            var state = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                state[reader.GetString(0)] = reader.GetInt64(1) != 0;
            }

            return state;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to load Host UI state for page {PageKey}.", pageKey);
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SetSectionState(string pageKey, string sectionKey, bool isExpanded)
    {
        if (string.IsNullOrWhiteSpace(pageKey) || string.IsNullOrWhiteSpace(sectionKey))
        {
            return;
        }

        try
        {
            using var connection = CreateOpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = $"""
INSERT INTO {TableName} (PageKey, SectionKey, IsExpanded, UpdatedAtUtc)
VALUES ($pageKey, $sectionKey, $isExpanded, $updatedAtUtc)
ON CONFLICT(PageKey, SectionKey) DO UPDATE SET
    IsExpanded = excluded.IsExpanded,
    UpdatedAtUtc = excluded.UpdatedAtUtc;
""";
            command.Parameters.AddWithValue("$pageKey", pageKey.Trim());
            command.Parameters.AddWithValue("$sectionKey", sectionKey.Trim());
            command.Parameters.AddWithValue("$isExpanded", isExpanded ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to save Host UI state for {PageKey}/{SectionKey}.", pageKey, sectionKey);
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

        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
CREATE TABLE IF NOT EXISTS {TableName} (
    PageKey TEXT NOT NULL,
    SectionKey TEXT NOT NULL,
    IsExpanded INTEGER NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    PRIMARY KEY (PageKey, SectionKey)
);
""";
        command.ExecuteNonQuery();
    }
}
