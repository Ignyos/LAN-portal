using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace Ignyos.LanPortal.Web.Services;

public sealed class FileClientTelemetry
{
    private static readonly Meter Meter = new("Ignyos.LanPortal.Web.Files", "1.0.0");

    private readonly Histogram<double> eventLagMs = Meter.CreateHistogram<double>(
        "files_client_event_lag_ms",
        unit: "ms",
        description: "Lag between event occurrence and client receipt");

    private readonly Counter<long> reconnectCount = Meter.CreateCounter<long>(
        "files_client_reconnect_count",
        description: "Count of SignalR reconnects");

    public void RecordEventLag(string eventType, double lagMs)
    {
        eventLagMs.Record(lagMs, new TagList { { "event_type", eventType } });
    }

    public void RecordReconnect(string state)
    {
        reconnectCount.Add(1, new TagList { { "state", state } });
    }
}
