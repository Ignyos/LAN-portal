namespace Ignyos.LanPortal.Api.Services;

public interface IUpdateManifestService
{
    Task<UpdateManifestFetchResult> GetLatestManifestAsync(bool forceRefresh, CancellationToken cancellationToken);
}

public sealed record UpdateManifestDocument(
    string Version,
    string Url,
    string Sha256,
    DateTimeOffset PublishedAt,
    string MinSupportedVersion);

public sealed record UpdateManifestFetchResult(
    string Channel,
    string ManifestUrl,
    DateTimeOffset CheckedAtUtc,
    bool IsStale,
    string? Error,
    UpdateManifestDocument? Manifest);
