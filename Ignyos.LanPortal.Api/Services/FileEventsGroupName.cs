namespace Ignyos.LanPortal.Api.Services;

public static class FileEventsGroupName
{
    private const string Prefix = "filescope:";
    private const string RootToken = "__root__";

    public static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim('/');

    public static string ForPath(string? path)
    {
        var normalized = NormalizePath(path);
        return Prefix + (string.IsNullOrWhiteSpace(normalized) ? RootToken : normalized.ToLowerInvariant());
    }

    public static string? ParentOfPath(string? path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }
}
