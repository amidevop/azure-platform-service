namespace AzurePlatformService.Shared.Models;

public record CircuitBreakerState
{
    public CircuitState State { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTime? LastFailureTime { get; init; }
    public DateTime? CircuitOpenedAt { get; init; }
}
