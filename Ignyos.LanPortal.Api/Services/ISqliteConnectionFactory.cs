using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public interface ISqliteConnectionFactory
{
    SqliteConnection CreateOpenConnection();
}

public sealed class SqliteConnectionFactory(IOptions<BootstrapOptions> bootstrapOptions) : ISqliteConnectionFactory
{
    public SqliteConnection CreateOpenConnection()
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
}
