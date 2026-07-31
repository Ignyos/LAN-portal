using System.Text.Json;
using Microsoft.JSInterop;

namespace Ignyos.LanPortal.Web.Services;

public sealed class AuthSession(IJSRuntime jsRuntime)
{
    private const string DefaultAuthenticatedPath = "/files";
    private const string RoleClaimUri = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    private readonly List<string> roles = [];

    public string? AccessToken { get; private set; }

    public DateTimeOffset? AccessTokenExpiresAtUtc { get; private set; }

    public string? RefreshToken { get; private set; }

    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; private set; }

    public string? LastVisitedPath { get; private set; }

    public IReadOnlyList<string> Roles => roles;

    public bool IsLoaded { get; private set; }

    /// <summary>Raised whenever the session is loaded, tokens are set, or the session is cleared.</summary>
    public event Action? SessionStateChanged;

    public bool IsAuthenticated =>
        IsLoaded &&
        !string.IsNullOrWhiteSpace(AccessToken) &&
        AccessTokenExpiresAtUtc is not null &&
        AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow;

    public bool HasActiveSession => IsAuthenticated || CanRefresh;

    public bool CanRefresh =>
        IsLoaded &&
        !string.IsNullOrWhiteSpace(RefreshToken) &&
        RefreshTokenExpiresAtUtc is not null &&
        RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow;

    public async Task InitializeAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        try
        {
            var stored = await jsRuntime.InvokeAsync<string?>("lanPortalAuth.get");
            LastVisitedPath = NormalizePath(await jsRuntime.InvokeAsync<string?>("lanPortalAuth.getLastPath"));

            if (!string.IsNullOrWhiteSpace(stored))
            {
                var snapshot = JsonSerializer.Deserialize<AuthSnapshot>(stored);
                AccessToken = snapshot?.AccessToken;
                AccessTokenExpiresAtUtc = snapshot?.AccessTokenExpiresAtUtc;
                RefreshToken = snapshot?.RefreshToken;
                RefreshTokenExpiresAtUtc = snapshot?.RefreshTokenExpiresAtUtc;
                roles.Clear();
                roles.AddRange(ExtractRoles(snapshot?.AccessToken));
            }
        }
        catch (JSException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        IsLoaded = true;
        SessionStateChanged?.Invoke();
    }

    public async Task SetTokensAsync(
        string accessToken,
        DateTimeOffset accessTokenExpiresAtUtc,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAtUtc)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
        IsLoaded = true;
        roles.Clear();
        roles.AddRange(ExtractRoles(accessToken));

        var snapshot = new AuthSnapshot(accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc);
        await jsRuntime.InvokeVoidAsync("lanPortalAuth.set", JsonSerializer.Serialize(snapshot));
        SessionStateChanged?.Invoke();
    }

    public Task SetTokenAsync(string accessToken, DateTimeOffset expiresAtUtc)
    {
        var refreshToken = RefreshToken;
        var refreshExpiresAtUtc = RefreshTokenExpiresAtUtc;

        if (string.IsNullOrWhiteSpace(refreshToken) || refreshExpiresAtUtc is null)
        {
            throw new InvalidOperationException("Cannot update access token without an active refresh token.");
        }

        return SetTokensAsync(accessToken, expiresAtUtc, refreshToken, refreshExpiresAtUtc.Value);
    }

    public async Task ClearAsync()
    {
        AccessToken = null;
        AccessTokenExpiresAtUtc = null;
        RefreshToken = null;
        RefreshTokenExpiresAtUtc = null;
        LastVisitedPath = null;
        IsLoaded = true;
        roles.Clear();

        try
        {
            await jsRuntime.InvokeVoidAsync("lanPortalAuth.remove");
            await jsRuntime.InvokeVoidAsync("lanPortalAuth.removeLastPath");
        }
        catch (JSException)
        {
        }
        SessionStateChanged?.Invoke();
    }

    public async Task RememberPathAsync(string path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Equals("/login", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LastVisitedPath = normalized;

        try
        {
            await jsRuntime.InvokeVoidAsync("lanPortalAuth.setLastPath", normalized);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public string GetPreferredAuthenticatedPath()
    {
        var normalized = NormalizePath(LastVisitedPath);
        return string.IsNullOrWhiteSpace(normalized) || normalized == "/"
            ? DefaultAuthenticatedPath
            : normalized;
    }

    public bool HasRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return roles.Any(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return trimmed;
    }

    private static IEnumerable<string> ExtractRoles(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return [];
        }

        var segments = accessToken.Split('.');
        if (segments.Length < 2)
        {
            return [];
        }

        try
        {
            var payloadBytes = DecodeBase64Url(segments[1]);
            using var payload = JsonDocument.Parse(payloadBytes);

            var results = new List<string>();
            AddRoles(payload.RootElement, "role", results);
            AddRoles(payload.RootElement, RoleClaimUri, results);

            return results
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void AddRoles(JsonElement root, string propertyName, List<string> destination)
    {
        if (!root.TryGetProperty(propertyName, out var roleElement))
        {
            return;
        }

        if (roleElement.ValueKind == JsonValueKind.String)
        {
            destination.Add(roleElement.GetString() ?? string.Empty);
            return;
        }

        if (roleElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var entry in roleElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                destination.Add(entry.GetString() ?? string.Empty);
            }
        }
    }

    private static byte[] DecodeBase64Url(string input)
    {
        var base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        var padding = base64.Length % 4;
        if (padding > 0)
        {
            base64 = base64.PadRight(base64.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(base64);
    }

    private sealed record AuthSnapshot(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAtUtc,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAtUtc);
}
