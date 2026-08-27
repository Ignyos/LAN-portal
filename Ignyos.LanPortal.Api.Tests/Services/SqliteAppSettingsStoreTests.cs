using Ignyos.LanPortal.Api;
using Ignyos.LanPortal.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ignyos.LanPortal.Api.Tests.Services;

public sealed class SqliteAppSettingsStoreTests
{
    [Fact]
    public void SetJwtConfigStoresNewValueAndRevokesActiveSessions()
    {
        using var database = new TemporaryDatabase();
        var settings = CreateStore(database);
        settings.Initialize();

        settings.RecordIssuedAccessSession(new AccessSessionRecord(
            Guid.NewGuid(),
            "jti-1",
            "alice",
            "Desktop",
            "User",
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddHours(1),
            null,
            null,
            DateTimeOffset.UtcNow));

        var newConfig = new JwtDatabaseConfig("Issuer-A", "Audience-A", "12345678901234567890123456789012");
        settings.SetJwtConfig(newConfig);

        var stored = settings.GetJwtConfig();
        Assert.Equal("Issuer-A", stored.Issuer);
        Assert.Equal("Audience-A", stored.Audience);
        Assert.Equal("12345678901234567890123456789012", stored.SigningKey);
        Assert.Empty(settings.GetActiveAccessSessions());
    }

    [Fact]
    public void RotateJwtSigningKeyChangesKeyAndRevokesActiveSessions()
    {
        using var database = new TemporaryDatabase();
        var settings = CreateStore(database);
        settings.Initialize();
        var originalKey = settings.GetJwtConfig().SigningKey;

        settings.RecordIssuedAccessSession(new AccessSessionRecord(
            Guid.NewGuid(),
            "jti-rotation",
            "alice",
            "Desktop",
            "User",
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddHours(1),
            null,
            null,
            DateTimeOffset.UtcNow));

        var result = settings.RotateJwtSigningKey();

        Assert.NotEqual(originalKey, settings.GetJwtConfig().SigningKey);
        Assert.Equal(1, result.RevokedSessionCount);
        Assert.Matches("^[0-9A-F]{16}$", result.KeyFingerprint);
        Assert.Empty(settings.GetActiveAccessSessions());
    }

    private static SqliteAppSettingsStore CreateStore(TemporaryDatabase database)
        => new(
            Options.Create(new BootstrapOptions { DatabasePath = database.Path }),
            new DpapiValueProtector());

    private sealed class TemporaryDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lanportal-settings-{Guid.NewGuid():N}.db");

        public void Dispose()
        {
            var files = new[] { Path, $"{Path}-shm", $"{Path}-wal" };
            foreach (var file in files)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // SQLite can leave the file locked briefly during teardown.
                }
            }
        }
    }
}
