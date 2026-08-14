using Ignyos.LanPortal.Api.Services;
using Xunit;

namespace Ignyos.LanPortal.Api.Tests.Services;

public sealed class StoragePathResolverTests
{
    [Fact]
    public void TryResolvePathUnderRoot_RejectsTraversalOutsideRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "lan-portal-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);

        var success = StoragePathResolver.TryResolvePathUnderRoot(root, "../outside.txt", out var fullPath);

        Assert.False(success);
        Assert.Null(fullPath);
    }

    [Fact]
    public void TryResolvePathUnderRoot_AcceptsSafeRelativePath()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "lan-portal-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);

        var success = StoragePathResolver.TryResolvePathUnderRoot(root, "folder/file.txt", out var fullPath);

        Assert.True(success);
        Assert.NotNull(fullPath);
        Assert.StartsWith(root, fullPath!, StringComparison.OrdinalIgnoreCase);
    }
}
