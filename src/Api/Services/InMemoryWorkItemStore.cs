using System.Collections.Concurrent;
using AzurePlatformService.Shared.Interfaces;
using AzurePlatformService.Shared.Models;

namespace AzurePlatformService.Api.Services;

public class InMemoryWorkItemStore : IWorkItemStore
{
    private readonly ConcurrentBag<WorkItem> _items = new();

    public Task AddAsync(WorkItem workItem)
    {
        _items.Add(workItem);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkItem>> GetRecentAsync(int limit)
    {
        var result = _items
            .OrderByDescending(w => w.ProcessedAt)
            .Take(limit)
            .ToList() as IReadOnlyList<WorkItem>;

        return Task.FromResult(result);
    }
}
