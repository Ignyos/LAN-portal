using Ignyos.LanPortal.Contracts;

namespace Ignyos.LanPortal.Api.Services;

public interface IFileEventPublisher
{
    Task PublishAsync(FileChangeEventDto fileEvent, CancellationToken cancellationToken = default);
}
