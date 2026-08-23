namespace Ignyos.LanPortal.Api;

public sealed class DeviceLoginOptions
{
    public const string SectionName = "DeviceLogin";

    public int RequestLifetimeSeconds { get; set; } = 5 * 60;
    public int PollIntervalSeconds { get; set; } = 3;
}
