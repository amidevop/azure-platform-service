namespace AzurePlatformService.Worker;

/// <summary>
/// Implements exponential backoff retry policy.
/// Formula: delay = baseDelay × multiplier^(attempt - 1)
/// With defaults (baseDelay=2s, multiplier=2, maxRetries=3): 2s, 4s, 8s
/// </summary>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly double _multiplier;

    public int MaxRetries { get; }

    public ExponentialBackoffRetryPolicy(
        TimeSpan? baseDelay = null,
        double multiplier = 2.0,
        int maxRetries = 3)
    {
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(2);
        _multiplier = multiplier;
        MaxRetries = maxRetries;
    }

    public TimeSpan ComputeDelay(int attempt)
    {
        if (attempt < 1)
            throw new ArgumentOutOfRangeException(nameof(attempt), "Attempt must be >= 1");

        // delay = baseDelay × multiplier^(attempt - 1)
        var delaySeconds = _baseDelay.TotalSeconds * Math.Pow(_multiplier, attempt - 1);
        return TimeSpan.FromSeconds(delaySeconds);
    }
}
