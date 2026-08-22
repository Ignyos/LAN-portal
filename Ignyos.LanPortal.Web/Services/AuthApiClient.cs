using System.Net;
using System.Net.Http.Json;
using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Web.Services;

public sealed class AuthApiClient(HttpClient httpClient)
{
    public async Task<DeviceLoginStartResponseDto> StartLoginAsync(
        DeviceLoginStartRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/device/request", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DeviceLoginStartResponseDto>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException("Device login start response was empty.");
    }

    public async Task<DeviceLoginPollResponseDto> PollLoginAsync(
        DeviceLoginPollRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/device/poll", request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<DeviceLoginPollResponseDto>(cancellationToken: cancellationToken);

        return payload ?? new DeviceLoginPollResponseDto("error", null, null, null, null, "Polling response was empty.");
    }

    public async Task<RefreshTokenResponseDto> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/token/refresh", request, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new SessionRevokedException();
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDto>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException("Refresh token response was empty.");
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "api/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        // Best-effort: ignore failures so client always clears local state.
    }
}
