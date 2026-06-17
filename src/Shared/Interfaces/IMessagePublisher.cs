using AzurePlatformService.Shared.Models;

namespace AzurePlatformService.Shared.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync(WorkItemMessage message, CancellationToken cancellationToken = default);
}
