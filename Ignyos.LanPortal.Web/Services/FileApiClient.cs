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
    public async Task<TreeNodeChildrenResponseDto> ListTreeChildrenAsync(string? parentPath, CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        var uri = string.IsNullOrWhiteSpace(parentPath)
            ? "api/files/tree/children"
            : $"api/files/tree/children?parentPath={Uri.EscapeDataString(parentPath)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyBearerToken(request);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        ThrowIfUnauthorized(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TreeNodeChildrenResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Tree children response was empty.");
    }

    public async Task<FileSearchResponseDto> SearchAsync(
        string query,
        string? searchRootPath,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/search")
        {
            Content = JsonContent.Create(new FileSearchRequestDto(query, searchRootPath, maxResults))
        };
        ApplyBearerToken(request);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        ThrowIfUnauthorized(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FileSearchResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Search response was empty.");
    }

    public async Task<FileNodeDto> CreateFolderAsync(
        string? currentPath,
        string name,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/folders")
        {
            Content = JsonContent.Create(new CreateFolderRequestDto(currentPath ?? string.Empty, name))
        };
        ApplyBearerToken(request);
        AddCorrelationId(request, correlationId);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        ThrowIfUnauthorized(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FileNodeDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Create folder response was empty.");
    }

    public async Task<FileNodeDto> RenameAsync(
        string path,
        string newName,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/rename")
        {
            Content = JsonContent.Create(new RenameItemRequestDto(path, newName))
        };
        ApplyBearerToken(request);
        AddCorrelationId(request, correlationId);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        ThrowIfUnauthorized(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FileNodeDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Rename response was empty.");
    }

    public async Task<IReadOnlyList<FileNodeDto>> MoveAsync(
        IReadOnlyList<string> paths,
        string destinationPath,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/move")
        {
            Content = JsonContent.Create(new MoveItemsRequestDto(paths, destinationPath))
        };
        ApplyBearerToken(request);
        AddCorrelationId(request, correlationId);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        ThrowIfUnauthorized(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<FileNodeDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task DeleteAsync(
        IReadOnlyList<string> paths,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/delete")
        {
            Content = JsonContent.Create(new DeleteItemsRequestDto(paths))
        };
        ApplyBearerToken(request);
        AddCorrelationId(request, correlationId);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        ThrowIfUnauthorized(response);
        response.EnsureSuccessStatusCode();
    }

    public async Task<FolderListResponseDto> ListFolderAsync(string? currentPath, CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        var uri = string.IsNullOrWhiteSpace(currentPath)
            ? "api/files/folder"
            : $"api/files/folder?currentPath={Uri.EscapeDataString(currentPath)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyBearerToken(request);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FolderListResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Folder list response was empty.");
    }

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
        string? currentPath = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        await using var fileStream = file.OpenReadStream(maxFileSizeBytes, cancellationToken);

        await EnsureAccessTokenAsync(cancellationToken);

        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", file.Name);
        content.Add(new StringContent(currentPath ?? string.Empty), "currentPath");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/upload")
        {
            Content = content
        };
        ApplyBearerToken(request);
        AddCorrelationId(request, correlationId);

        using var response = await SendWithRefreshRetryAsync(request, cancellationToken);
        ThrowIfUnauthorized(response);

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

    private static void AddCorrelationId(HttpRequestMessage request, string? correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.Add("X-Correlation-ID", correlationId);
        }
    }

    private static void ThrowIfUnauthorized(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }
    }

}
