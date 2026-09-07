using System.IO;
using Ignyos.LanPortal.Api.Services;
using Xunit;

namespace Ignyos.LanPortal.Api.Tests.Services;

public class AssetVersionServiceTests
{
    [Fact]
    public void GetVersionedUrl_UsesStableContentHash_ForSameFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lan-portal-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, "host.css");
            File.WriteAllText(filePath, "body { color: red; }\n");

            var version1 = AssetVersionService.GetVersionedUrl(filePath, "/host.css");
            var version2 = AssetVersionService.GetVersionedUrl(filePath, "/host.css");

            Assert.Equal(version1, version2);
            Assert.Contains("?v=", version1, StringComparison.Ordinal);
            Assert.StartsWith("/host.css?v=", version1, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetVersionedUrl_ReturnsOriginalPath_WhenFileMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "host.css");

        var result = AssetVersionService.GetVersionedUrl(missingPath, "/host.css");

        Assert.Equal("/host.css", result);
    }
}
