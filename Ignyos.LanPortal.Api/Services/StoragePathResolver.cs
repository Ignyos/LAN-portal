namespace Ignyos.LanPortal.Api.Services;

public static class StoragePathResolver
{
    public static string EnsureStorageRoot(string? configuredRootPath)
    {
        var rootPath = string.IsNullOrWhiteSpace(configuredRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "storage")
            : configuredRootPath;

        rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    public static string ToRelativePath(string rootPath, string fullPath)
    {
        var relative = Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');
        return relative == "." ? string.Empty : relative;
    }

    public static bool TryResolveOptionalPathUnderRoot(string rootPath, string? relativePath, out string? fullPath)
    {
        fullPath = null;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            fullPath = rootPath;
            return true;
        }

        return TryResolvePathUnderRoot(rootPath, relativePath, out fullPath);
    }

    public static string GetUniquePath(string rootPath, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(rootPath, fileName);
        var counter = 1;

        while (System.IO.File.Exists(candidate))
        {
            candidate = Path.Combine(rootPath, $"{baseName}-{counter}{extension}");
            counter++;
        }

        return candidate;
    }

    public static bool TryResolvePathUnderRoot(string rootPath, string relativePath, out string? fullPath)
    {
        fullPath = null;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var combined = Path.Combine(rootPath, normalizedRelativePath);
        var candidatePath = Path.GetFullPath(combined);
        var relativeFromRoot = Path.GetRelativePath(rootPath, candidatePath);

        if (relativeFromRoot.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeFromRoot))
        {
            return false;
        }

        fullPath = candidatePath;
        return true;
    }
}
