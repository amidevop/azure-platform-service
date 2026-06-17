using AzurePlatformService.Shared.Models;

namespace AzurePlatformService.Api.Services;

/// <summary>
/// Circuit breaker for Service Bus calls. Opens after 5 consecutive failures
/// within a 60-second window, rejects calls in open state with 503,
/// and probes after 30 seconds in half-open state.
/// </summary>
public class CircuitBreakerService
{
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTime? _lastFailureTime;
    private DateTime? _circuitOpenedAt;

    private const int FailureThreshold = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OpenStateDuration = TimeSpan.FromSeconds(30);

    public CircuitBreakerService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets a snapshot of the current circuit breaker state.
    /// </summary>
    public CircuitBreakerState GetState()
    {
        lock (_lock)
        {
            return new CircuitBreakerState
            {
                State = _state,
                ConsecutiveFailures = _consecutiveFailures,
                LastFailureTime = _lastFailureTime,
                CircuitOpenedAt = _circuitOpenedAt
            };
        }
    }

    /// <summary>
    /// Executes an action through the circuit breaker. Throws CircuitBreakerOpenException
    /// if the circuit is open and the timeout has not elapsed.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        EnsureCircuitAllowsRequest();

        try
        {
            var result = await action(cancellationToken);
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    /// <summary>
    /// Executes an action through the circuit breaker (void return).
    /// Throws CircuitBreakerOpenException if the circuit is open and the timeout has not elapsed.
    /// </summary>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        EnsureCircuitAllowsRequest();

        try
        {
            await action(cancellationToken);
            OnSuccess();
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    private void EnsureCircuitAllowsRequest()
    {
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            switch (_state)
            {
                case CircuitState.Closed:
                    // Allow request
                    break;

                case CircuitState.Open:
                    if (_circuitOpenedAt.HasValue &&
                        (now - _circuitOpenedAt.Value) >= OpenStateDuration)
                    {
                        // Transition to half-open, allow one probe request
                        _state = CircuitState.HalfOpen;
                    }
                    else
                    {
                        throw new CircuitBreakerOpenException(
                            "Circuit breaker is open. Service Bus calls are temporarily unavailable.");
                    }
                    break;

                case CircuitState.HalfOpen:
                    // Only one probe request is allowed at a time.
                    // Transition to Open to block additional requests while probe is in flight.
                    // If probe succeeds, OnSuccess will set state to Closed.
                    // If probe fails, OnFailure will keep it Open.
                    _state = CircuitState.Open;
                    _circuitOpenedAt = now;
                    break;
            }
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _consecutiveFailures = 0;
            _lastFailureTime = null;
            _circuitOpenedAt = null;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (_state == CircuitState.HalfOpen || _state == CircuitState.Open)
            {
                // Probe failed or call in open state failed — re-open the circuit
                _state = CircuitState.Open;
                _circuitOpenedAt = now;
                return;
            }

            // Closed state: track consecutive failures within the window
            if (_lastFailureTime.HasValue && (now - _lastFailureTime.Value) > FailureWindow)
            {
                // Previous failure was outside the window; reset counter
                _consecutiveFailures = 1;
            }
            else
            {
                _consecutiveFailures++;
            }

            _lastFailureTime = now;

            if (_consecutiveFailures >= FailureThreshold)
            {
                _state = CircuitState.Open;
                _circuitOpenedAt = now;
            }
        }
    }
}
