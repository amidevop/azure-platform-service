namespace AzurePlatformService.Shared.Models;

public record DependencyHealth
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Healthy";
    public string? Reason { get; init; }
}
