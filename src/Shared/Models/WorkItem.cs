namespace AzurePlatformService.Shared.Models;

public record WorkItem
{
    public Guid Id { get; init; }
    public string Payload { get; init; } = string.Empty;
    public DateTime? ProcessedAt { get; init; }
    public WorkItemStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}
