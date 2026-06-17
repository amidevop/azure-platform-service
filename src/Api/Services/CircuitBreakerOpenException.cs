namespace AzurePlatformService.Api.Services;

/// <summary>
/// Exception thrown when the circuit breaker is in open state,
/// indicating that calls should not be attempted.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException()
        : base("Circuit breaker is open. Service is temporarily unavailable.")
    {
    }

    public CircuitBreakerOpenException(string message)
        : base(message)
    {
    }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
