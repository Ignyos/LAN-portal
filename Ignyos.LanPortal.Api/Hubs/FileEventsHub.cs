using Ignyos.LanPortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ignyos.LanPortal.Api.Hubs;

[Authorize]
public sealed class FileEventsHub : Hub
{
    public async Task SubscribePaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var groupName = FileEventsGroupName.ForPath(path);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
    }

    public async Task UnsubscribePaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var groupName = FileEventsGroupName.ForPath(path);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
    }
}
