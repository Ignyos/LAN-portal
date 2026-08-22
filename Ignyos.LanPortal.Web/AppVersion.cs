namespace Ignyos.LanPortal.Web;

/// <summary>Cache-busting token that changes on every app start.</summary>
public static class AppVersion
{
    public static readonly string Token = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
}
