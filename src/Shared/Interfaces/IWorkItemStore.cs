using AzurePlatformService.Shared.Models;

namespace AzurePlatformService.Shared.Interfaces;

public interface IWorkItemStore
{
    Task AddAsync(WorkItem workItem);
    Task<IReadOnlyList<WorkItem>> GetRecentAsync(int limit);
}
