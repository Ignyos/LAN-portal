using Ignyos.LanPortal.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Ignyos.LanPortal.Web.Services;

public sealed class FileEventsClient(
    AuthSession authSession,
    NavigationManager navigationManager,
    IConfiguration configuration,
    FileClientTelemetry telemetry) : IAsyncDisposable
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private HubConnection? connection;
    private HashSet<string> subscribedPaths = new(StringComparer.OrdinalIgnoreCase);

    public event Action<FileChangeEventDto>? FileChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await authSession.InitializeAsync();

            if (connection is null)
            {
                connection = BuildConnection();
                connection.On<FileChangeEventDto>("fileChanged", change =>
                {
                    var lagMs = (DateTimeOffset.UtcNow - change.OccurredAtUtc).TotalMilliseconds;
                    telemetry.RecordEventLag(change.EventType, Math.Max(0, lagMs));
                    FileChanged?.Invoke(change);
                });

                connection.Reconnecting += _ =>
                {
                    telemetry.RecordReconnect("reconnecting");
                    return Task.CompletedTask;
                };

                connection.Reconnected += _ =>
                {
                    telemetry.RecordReconnect("reconnected");
                    return Task.CompletedTask;
                };

                connection.Closed += _ =>
                {
                    telemetry.RecordReconnect("closed");
                    return Task.CompletedTask;
                };
            }

            if (connection.State is HubConnectionState.Connected or HubConnectionState.Connecting)
            {
                return;
            }

            try
            {
                await connection.StartAsync(cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new SessionRevokedException();
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task SetSubscriptionsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        await lifecycleGate.WaitAsync(cancellationToken);
        try
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
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await lifecycleGate.WaitAsync();
        try
        {
            if (connection is null)
            {
                return;
            }

            var connectionToDispose = connection;
            connection = null;
            subscribedPaths.Clear();

            try
            {
                await connectionToDispose.DisposeAsync();
            }
            catch
            {
            }
        }
        finally
        {
            lifecycleGate.Release();
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
