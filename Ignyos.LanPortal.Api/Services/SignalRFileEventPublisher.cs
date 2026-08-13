using Ignyos.LanPortal.Api.Hubs;
using Ignyos.LanPortal.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Ignyos.LanPortal.Api.Services;

public sealed class SignalRFileEventPublisher(IHubContext<FileEventsHub> hubContext) : IFileEventPublisher
{
    public async Task PublishAsync(FileChangeEventDto fileEvent, CancellationToken cancellationToken = default)
    {
        var scopePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FileEventsGroupName.NormalizePath(fileEvent.ScopePath),
            FileEventsGroupName.ParentOfPath(fileEvent.FromPath) ?? string.Empty,
            FileEventsGroupName.ParentOfPath(fileEvent.ToPath) ?? string.Empty,
            string.Empty
        };

        foreach (var scopePath in scopePaths)
        {
            var groupName = FileEventsGroupName.ForPath(scopePath);
            await hubContext.Clients.Group(groupName).SendAsync("fileChanged", fileEvent, cancellationToken);
        }
    }
}
