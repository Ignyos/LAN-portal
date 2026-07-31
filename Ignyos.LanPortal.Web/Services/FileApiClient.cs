using System.Net.Http.Json;
using System.Net.Http.Headers;
using Ignyos.LanPortal.Contracts;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;

namespace Ignyos.LanPortal.Web.Services;

public sealed class FileApiClient(
    HttpClient httpClient,
    AuthSession authSession,
    AuthApiClient authApiClient,
    NavigationManager navigationManager,
    IConfiguration configuration)
{
    public async Task<IReadOnlyList<FileEntryDto>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/files");
        ApplyBearerToken(request);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        response.EnsureSuccessStatusCode();

        var files = await response.Content.ReadFromJsonAsync<List<FileEntryDto>>(cancellationToken: cancellationToken) ?? [];

        return files;
    }

    public async Task<UploadResultDto> UploadAsync(
        IBrowserFile file,
        long maxFileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        await using var fileStream = file.OpenReadStream(maxFileSizeBytes, cancellationToken);

        await EnsureAccessTokenAsync(cancellationToken);

        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", file.Name);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/upload")
        {
            Content = content
        };
        ApplyBearerToken(request);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultDto>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Upload response was empty.");
    }

    public string BuildDownloadUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(authSession.AccessToken))
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        var encodedSegments = relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        var encodedToken = Uri.EscapeDataString(authSession.AccessToken);

        var configuredPublicBase = configuration["Api:PublicBaseUrl"];
        var downloadBase = !string.IsNullOrWhiteSpace(configuredPublicBase)
            ? configuredPublicBase
            : BuildApiBaseFromCurrentHost();

        return $"{downloadBase.TrimEnd('/')}/api/files/download/{string.Join('/', encodedSegments)}?access_token={encodedToken}";
    }

    private string BuildApiBaseFromCurrentHost()
    {
        var current = new Uri(navigationManager.Uri);
        var host = current.Host;

        if (current.Port == 5212)
        {
            return $"{current.Scheme}://{host}:5212";
        }

        return $"http://{host}:5212";
    }

    private void ApplyBearerToken(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(authSession.AccessToken))
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authSession.AccessToken);
    }

    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        await authSession.InitializeAsync();

        if (authSession.IsAuthenticated)
        {
            return;
        }

        if (!authSession.CanRefresh || string.IsNullOrWhiteSpace(authSession.RefreshToken))
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        await RefreshAccessTokenAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRefreshRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is not (System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden))
        {
            return response;
        }

        response.Dispose();

        if (!authSession.CanRefresh || string.IsNullOrWhiteSpace(authSession.RefreshToken))
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        }

        if (request.Method != HttpMethod.Get || request.Content is not null)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        }

        await RefreshAccessTokenAsync(cancellationToken);

        var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        ApplyBearerToken(retry);
        return await httpClient.SendAsync(retry, cancellationToken);
    }

    private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var payload = await authApiClient.RefreshTokenAsync(
            new RefreshTokenRequestDto(authSession.RefreshToken ?? string.Empty),
            cancellationToken);

        await authSession.SetTokensAsync(
            payload.AccessToken,
            payload.AccessTokenExpiresAtUtc,
            payload.RefreshToken,
            payload.RefreshTokenExpiresAtUtc);
    }

}
