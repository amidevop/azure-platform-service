namespace AzurePlatformService.Shared.Models;

public record HealthCheckResponse
{
    public string Status { get; init; } = "Healthy";
    public IReadOnlyList<DependencyHealth>? Dependencies { get; init; }
}
