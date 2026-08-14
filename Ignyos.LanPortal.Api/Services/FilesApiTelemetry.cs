using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace Ignyos.LanPortal.Api.Services;

public sealed class FilesApiTelemetry
{
    private static readonly Meter Meter = new("Ignyos.LanPortal.Api.Files", "1.0.0");

    private readonly Histogram<double> requestDurationMs = Meter.CreateHistogram<double>(
        "files_api_request_duration_ms",
        unit: "ms",
        description: "Duration of /api/files requests");

    private readonly Counter<long> requestCount = Meter.CreateCounter<long>(
        "files_api_request_count",
        description: "Count of /api/files requests");

    public void RecordRequest(string operation, int statusCode, double elapsedMs)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "status_code", statusCode }
        };

        requestCount.Add(1, tags);
        requestDurationMs.Record(elapsedMs, tags);
    }
}
