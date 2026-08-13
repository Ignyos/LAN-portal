using Ignyos.LanPortal.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Ignyos.LanPortal.Web.Services;

public sealed class FileEventsClient(AuthSession authSession, NavigationManager navigationManager, IConfiguration configuration) : IAsyncDisposable
{
    private HubConnection? connection;
    private HashSet<string> subscribedPaths = new(StringComparer.OrdinalIgnoreCase);

    public event Action<FileChangeEventDto>? FileChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await authSession.InitializeAsync();

        if (connection is null)
        {
            connection = BuildConnection();
            connection.On<FileChangeEventDto>("fileChanged", change => FileChanged?.Invoke(change));
        }

        if (connection.State is HubConnectionState.Connected or HubConnectionState.Connecting)
        {
            return;
        }

        await connection.StartAsync(cancellationToken);
    }

    public async Task SetSubscriptionsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            return;
        }

        var desiredPaths = paths
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toUnsubscribe = subscribedPaths.Except(desiredPaths, StringComparer.OrdinalIgnoreCase).ToArray();
        var toSubscribe = desiredPaths.Except(subscribedPaths, StringComparer.OrdinalIgnoreCase).ToArray();

        if (toUnsubscribe.Length > 0)
        {
            await connection.InvokeAsync("UnsubscribePaths", toUnsubscribe, cancellationToken);
        }

        if (toSubscribe.Length > 0)
        {
            await connection.InvokeAsync("SubscribePaths", toSubscribe, cancellationToken);
        }

        subscribedPaths = desiredPaths;
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is null)
        {
            return;
        }

        try
        {
            await connection.DisposeAsync();
        }
        catch
        {
        }
    }

    private HubConnection BuildConnection()
    {
        var hubUrl = BuildHubUrl();
        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(authSession.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();
    }

    private string BuildHubUrl()
    {
        var configuredPublicBase = configuration["Api:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configuredPublicBase))
        {
            return $"{configuredPublicBase.TrimEnd('/')}/hubs/files";
        }

        var current = new Uri(navigationManager.Uri);
        var host = current.Host;

        if (current.Port == 5212)
        {
            return $"{current.Scheme}://{host}:5212/hubs/files";
        }

        return $"http://{host}:5212/hubs/files";
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim('/');
}
