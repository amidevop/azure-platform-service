namespace AzurePlatformService.Worker;

/// <summary>
/// Defines the retry policy for transient error handling.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts before giving up.
    /// </summary>
    int MaxRetries { get; }

    /// <summary>
    /// Computes the delay before the next retry attempt.
    /// </summary>
    /// <param name="attempt">The current attempt number (1-based).</param>
    /// <returns>The time to wait before the next retry.</returns>
    TimeSpan ComputeDelay(int attempt);
}
