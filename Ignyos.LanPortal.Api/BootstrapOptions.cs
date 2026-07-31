namespace Ignyos.LanPortal.Api;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string DatabasePath { get; set; } = "data/lanportal.db";
}
