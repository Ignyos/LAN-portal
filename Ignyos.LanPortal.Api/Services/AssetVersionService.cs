using System.Security.Cryptography;
using System.Text;

namespace Ignyos.LanPortal.Api.Services;

public static class AssetVersionService
{
    public static string GetVersionedUrl(string? physicalPath, string requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return requestPath;
        }

        if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
        {
            return requestPath;
        }

        var hash = ComputeHash(physicalPath);
        return $"{requestPath}?v={hash}";
    }

    private static string ComputeHash(string physicalPath)
    {
        var bytes = File.ReadAllBytes(physicalPath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
