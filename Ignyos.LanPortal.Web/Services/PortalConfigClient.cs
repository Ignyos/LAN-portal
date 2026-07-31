using System.Net.Http.Json;

namespace Ignyos.LanPortal.Web.Services;

public sealed class PortalConfigClient(HttpClient httpClient)
{
    private const string DefaultNetworkName = "My Home";
    private string? cachedNetworkName;

    public async Task<string> GetNetworkNameAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(cachedNetworkName))
        {
            return cachedNetworkName;
        }

        try
        {
            var payload = await httpClient.GetFromJsonAsync<PortalConfigResponse>("api/public/portal-config", cancellationToken);
            cachedNetworkName = string.IsNullOrWhiteSpace(payload?.NetworkName)
                ? DefaultNetworkName
                : payload.NetworkName.Trim();
        }
        catch
        {
            cachedNetworkName = DefaultNetworkName;
        }

        return cachedNetworkName;
    }

    private sealed record PortalConfigResponse(string? NetworkName);
}