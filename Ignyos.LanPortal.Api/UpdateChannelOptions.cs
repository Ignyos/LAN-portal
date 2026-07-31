namespace Ignyos.LanPortal.Api;

public sealed class UpdateChannelOptions
{
    public const string SectionName = "UpdateChannel";

    public string BaseUrl { get; set; } = "https://lanportal.ignyos.com";

    public string Channel { get; set; } = "production";

    public string ProductionManifestPath { get; set; } = "/updates/manifest.json";

    public string TestManifestPath { get; set; } = "/updates/manifest-test.json";

    public int PollIntervalMinutes { get; set; } = 60;
}
