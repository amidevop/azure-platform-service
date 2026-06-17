namespace AzurePlatformService.Shared.Models;

public record WorkItemMessage
{
    public Guid WorkItemId { get; init; }
    public string Payload { get; init; } = string.Empty;
    public DateTime EnqueuedAt { get; init; }
    public int AttemptCount { get; init; }
}
