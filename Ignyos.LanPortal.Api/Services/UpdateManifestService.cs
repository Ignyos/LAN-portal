using System.Text.Json;
using Ignyos.LanPortal.Api;
using Microsoft.Extensions.Options;

namespace Ignyos.LanPortal.Api.Services;

public sealed class UpdateManifestService(
    IOptionsMonitor<UpdateChannelOptions> optionsMonitor,
    IHttpClientFactory httpClientFactory,
    ILogger<UpdateManifestService> logger) : IUpdateManifestService
{
    private readonly object syncRoot = new();
    private UpdateManifestDocument? cachedManifest;
    private DateTimeOffset lastCheckedAtUtc = DateTimeOffset.MinValue;
    private string? cachedManifestUrl;

    public async Task<UpdateManifestFetchResult> GetLatestManifestAsync(
        bool forceRefresh,
        bool isDeveloperInstaller,
        CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        var channel = isDeveloperInstaller ? "test" : "production";
        var baseUrl = ResolveBaseUrl(options, isDeveloperInstaller);
        var manifestUrl = BuildManifestUrl(baseUrl, options, channel);
        var pollInterval = NormalizePollInterval(options.PollIntervalMinutes);

        lock (syncRoot)
        {
            var hasFreshCache =
                !forceRefresh &&
                cachedManifest is not null &&
                string.Equals(cachedManifestUrl, manifestUrl, StringComparison.OrdinalIgnoreCase) &&
                DateTimeOffset.UtcNow - lastCheckedAtUtc < pollInterval;

            if (hasFreshCache)
            {
                return new UpdateManifestFetchResult(
                    channel,
                    manifestUrl,
                    lastCheckedAtUtc,
                    false,
                    null,
                    cachedManifest);
            }
        }

        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            using var response = await httpClient.GetAsync(manifestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = await JsonSerializer.DeserializeAsync<UpdateManifestDocument>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (document is null ||
                string.IsNullOrWhiteSpace(document.Version) ||
                string.IsNullOrWhiteSpace(document.Url) ||
                string.IsNullOrWhiteSpace(document.Sha256) ||
                string.IsNullOrWhiteSpace(document.MinSupportedVersion))
            {
                throw new InvalidOperationException("Manifest payload is missing one or more required fields.");
            }

            lock (syncRoot)
            {
                cachedManifest = document;
                cachedManifestUrl = manifestUrl;
                lastCheckedAtUtc = DateTimeOffset.UtcNow;

                return new UpdateManifestFetchResult(
                    channel,
                    manifestUrl,
                    lastCheckedAtUtc,
                    false,
                    null,
                    cachedManifest);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Update manifest fetch failed for channel '{Channel}' from {ManifestUrl}", channel, manifestUrl);

            lock (syncRoot)
            {
                if (cachedManifest is not null &&
                    string.Equals(cachedManifestUrl, manifestUrl, StringComparison.OrdinalIgnoreCase))
                {
                    return new UpdateManifestFetchResult(
                        channel,
                        cachedManifestUrl ?? manifestUrl,
                        DateTimeOffset.UtcNow,
                        true,
                        ex.Message,
                        cachedManifest);
                }
            }

            return new UpdateManifestFetchResult(
                channel,
                manifestUrl,
                DateTimeOffset.UtcNow,
                false,
                ex.Message,
                null);
        }
    }

    private static TimeSpan NormalizePollInterval(int pollIntervalMinutes)
    {
        var clampedMinutes = Math.Clamp(pollIntervalMinutes, 1, 24 * 60);
        return TimeSpan.FromMinutes(clampedMinutes);
    }

    private static string ResolveBaseUrl(UpdateChannelOptions options, bool isDeveloperInstaller)
    {
        if (isDeveloperInstaller)
        {
            return string.IsNullOrWhiteSpace(options.DevBaseUrl)
                ? "https://lanportal-dev.ignyos.com"
                : options.DevBaseUrl.TrimEnd('/');
        }

        return string.IsNullOrWhiteSpace(options.ProductionBaseUrl)
            ? "https://lanportal.ignyos.com"
            : options.ProductionBaseUrl.TrimEnd('/');
    }

    private static string BuildManifestUrl(string baseUrl, UpdateChannelOptions options, string channel)
    {
        var manifestPath = channel == "test"
            ? options.TestManifestPath
            : options.ProductionManifestPath;

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            manifestPath = channel == "test" ? "/updates/manifest-test.json" : "/updates/manifest.json";
        }

        if (!manifestPath.StartsWith('/'))
        {
            manifestPath = "/" + manifestPath;
        }

        return baseUrl + manifestPath;
    }
}
