namespace AzurePlatformService.Shared.Interfaces;

/// <summary>
/// Interface for checking the health of a downstream dependency.
/// Implementations should return within the specified timeout.
/// </summary>
public interface IDependencyHealthCheck
{
    /// <summary>
    /// The name of the dependency being checked (e.g., "ServiceBus", "ApplicationInsights").
    /// </summary>
    string DependencyName { get; }

    /// <summary>
    /// Checks the health of the dependency within the specified timeout.
    /// </summary>
    /// <param name="timeout">Maximum time allowed for the health check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if healthy, false otherwise.</returns>
    Task<(bool IsHealthy, string? Reason)> CheckHealthAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
