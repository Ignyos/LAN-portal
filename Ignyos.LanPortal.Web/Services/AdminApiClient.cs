using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Web.Services;

public sealed class AdminApiClient(HttpClient httpClient, AuthSession authSession, AuthApiClient authApiClient)
{
    public async Task<WhoAmIResponseDto> GetWhoAmIAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/admin/whoami"),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<WhoAmIResponseDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("WhoAmI response was empty.");
    }

    public async Task<IReadOnlyList<PendingLoginRequestDto>> ListPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/admin/approvals/pending"),
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this admin feature.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<PendingLoginRequestDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task ApproveLoginAsync(Guid requestId, ApproveLoginRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"api/admin/approvals/{requestId:D}/approve")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this admin feature.");
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task DenyLoginAsync(Guid requestId, DenyLoginRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"api/admin/approvals/{requestId:D}/deny")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this admin feature.");
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<AccessSessionDto>> ListActiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/admin/sessions/active"),
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this admin feature.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<AccessSessionDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task RevokeSessionAsync(Guid sessionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"api/admin/sessions/{sessionId:D}/revoke")
            {
                Content = JsonContent.Create(new RevokeSessionRequestDto(reason))
            },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this admin feature.");
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<int> RevokeByFilterAsync(string? userName, string? deviceName, string? reason = null, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "api/admin/sessions/revoke-by-filter")
            {
                Content = JsonContent.Create(new RevokeByFilterRequestDto(userName, deviceName, reason))
            },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this admin feature.");
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RevokeByFilterResponseDto>(cancellationToken: cancellationToken);
        return payload?.RevokedCount ?? 0;
    }

    public async Task<UpdateSessionRolesResponseDto> UpdateSessionRolesAsync(
        Guid sessionId,
        string roles,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"api/admin/sessions/{sessionId:D}/roles")
            {
                Content = JsonContent.Create(new UpdateSessionRolesRequestDto(roles, reason))
            },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this admin feature.");
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UpdateSessionRolesResponseDto>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException("Update roles response was empty.");
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        var response = await SendAuthorizedRequestAsync(requestFactory, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
        {
            return response;
        }

        response.Dispose();

        if (!authSession.CanRefresh)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        await RefreshAccessTokenAsync(cancellationToken);
        var retry = await SendAuthorizedRequestAsync(requestFactory, cancellationToken);
        return retry;
    }

    private async Task<HttpResponseMessage> SendAuthorizedRequestAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        ApplyBearerToken(request);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        await authSession.InitializeAsync();

        if (authSession.IsAuthenticated)
        {
            return;
        }

        if (!authSession.CanRefresh)
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        await RefreshAccessTokenAsync(cancellationToken);
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

    private void ApplyBearerToken(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(authSession.AccessToken))
        {
            throw new UnauthorizedAccessException("Your session is no longer valid. Please log in again.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authSession.AccessToken);
    }
}
