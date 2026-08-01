using System.Net;
using System.Reflection;
using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;

namespace Ignyos.LanPortal.Api.Controllers;

[ApiController]
public sealed class LocalUpdateController(IUpdateManifestService updateManifestService) : ControllerBase
{
    [HttpGet("api/local/update/status")]
    public async Task<IActionResult> Status([FromQuery] string? currentVersion, CancellationToken cancellationToken)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var resolvedCurrentVersion = string.IsNullOrWhiteSpace(currentVersion)
            ? GetDisplayVersion()
            : currentVersion;

        return Ok(await BuildResponseAsync(resolvedCurrentVersion, forceRefresh: false, cancellationToken));
    }

    [HttpPost("api/local/update/check-now")]
    public async Task<IActionResult> CheckNow([FromBody] CheckNowRequest? request, CancellationToken cancellationToken)
    {
        if (!IsLocalRequest(HttpContext))
        {
            return NotFound();
        }

        var resolvedCurrentVersion = string.IsNullOrWhiteSpace(request?.CurrentVersion)
            ? GetDisplayVersion()
            : request!.CurrentVersion;

        return Ok(await BuildResponseAsync(resolvedCurrentVersion, forceRefresh: true, cancellationToken));
    }

    private async Task<UpdateStatusResponse> BuildResponseAsync(
        string currentVersion,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var fetchResult = await updateManifestService.GetLatestManifestAsync(forceRefresh, cancellationToken);

        var isTestChannel = string.Equals(fetchResult.Channel, "test", StringComparison.OrdinalIgnoreCase);
        var manifest = fetchResult.Manifest;

        var latestVersion = manifest?.Version;
        var minSupportedVersion = manifest?.MinSupportedVersion;

        var updateAvailable = false;
        var requiredUpdate = false;

        if (TryParseSemVer(currentVersion, out var currentSemVer) &&
            TryParseSemVer(latestVersion, out var latestSemVer))
        {
            updateAvailable = currentSemVer < latestSemVer;
        }

        if (TryParseSemVer(currentVersion, out currentSemVer) &&
            TryParseSemVer(minSupportedVersion, out var minSupportedSemVer))
        {
            requiredUpdate = currentSemVer < minSupportedSemVer;
        }

        return new UpdateStatusResponse(
            currentVersion,
            latestVersion,
            minSupportedVersion,
            manifest?.Url,
            manifest?.Sha256,
            updateAvailable,
            requiredUpdate,
            fetchResult.Channel,
            isTestChannel,
            fetchResult.ManifestUrl,
            fetchResult.CheckedAtUtc,
            fetchResult.IsStale,
            fetchResult.Error);
    }

    private static bool TryParseSemVer(string? candidate, out NuGetVersion version)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && NuGetVersion.TryParse(candidate, out version!))
        {
            return true;
        }

        version = new NuGetVersion(0, 0, 0);
        return false;
    }

    private static bool IsLocalRequest(HttpContext httpContext)
    {
        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteIpAddress is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(remoteIpAddress))
        {
            return true;
        }

        if (remoteIpAddress.IsIPv4MappedToIPv6)
        {
            var mapped = remoteIpAddress.MapToIPv4();
            if (IPAddress.IsLoopback(mapped))
            {
                return true;
            }
        }

        var localIpAddress = httpContext.Connection.LocalIpAddress;
        return localIpAddress is not null && remoteIpAddress.Equals(localIpAddress);
    }

    private static string GetDisplayVersion()
    {
        var informational = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+', 2)[0].Trim();
        }

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
    }

    public sealed record CheckNowRequest(string? CurrentVersion);

    public sealed record UpdateStatusResponse(
        string CurrentVersion,
        string? LatestVersion,
        string? MinSupportedVersion,
        string? DownloadUrl,
        string? ExpectedSha256,
        bool UpdateAvailable,
        bool RequiredUpdate,
        string Channel,
        bool IsTestChannel,
        string ManifestUrl,
        DateTimeOffset CheckedAtUtc,
        bool IsStale,
        string? Error);
}
