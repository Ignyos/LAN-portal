using System.Diagnostics;

namespace Ignyos.LanPortal.Api.Services;

public sealed class FilesRequestMetricsMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context, FilesApiTelemetry telemetry)
    {
        if (!context.Request.Path.StartsWithSegments("/api/files", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        var operation = ResolveOperationName(context.Request.Path);
        telemetry.RecordRequest(operation, context.Response.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static string ResolveOperationName(PathString requestPath)
    {
        var path = requestPath.Value?.ToLowerInvariant() ?? string.Empty;

        if (path.StartsWith("/api/files/folder")) return "folder.list";
        if (path.StartsWith("/api/files/tree/children")) return "tree.children";
        if (path.StartsWith("/api/files/search")) return "search";
        if (path.StartsWith("/api/files/folders")) return "folder.create";
        if (path.StartsWith("/api/files/rename")) return "item.rename";
        if (path.StartsWith("/api/files/move")) return "item.move";
        if (path.StartsWith("/api/files/delete")) return "item.delete";
        if (path.StartsWith("/api/files/upload")) return "upload";
        if (path.StartsWith("/api/files/download")) return "download";

        return "list.legacy";
    }
}
