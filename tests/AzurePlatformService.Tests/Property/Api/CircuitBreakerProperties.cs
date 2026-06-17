using AzurePlatformService.Api.Services;
using AzurePlatformService.Shared.Models;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AzurePlatformService.Tests.Property.Api;

/// <summary>
/// Property-based tests for CircuitBreakerService state transitions and behavior.
/// </summary>
[Trait("Feature", "azure-platform-service")]
public class CircuitBreakerProperties
{
    /// <summary>
    /// Property 9: Circuit breaker opens after consecutive failures.
    /// Generate sequences of success/failure results, verify transitions occur at exactly 5 consecutive failures.
    /// **Validates: Requirements 12.4**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(BoolSequenceArbitrary) })]
    public FsCheck.Property CircuitBreakerOpensAfterExactlyFiveConsecutiveFailures(bool[] outcomes)
    {
        return Prop.ForAll(Arb.Default.Bool(), _ =>
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerService();
            int consecutiveFailures = 0;
            bool hasOpened = false;

            // Act
            foreach (var outcome in outcomes)
            {
                var state = circuitBreaker.GetState();
                if (state.State == CircuitState.Open)
                {
                    hasOpened = true;
                    break;
                }

                if (outcome)
                {
                    // Success: execute action that succeeds
                    try
                    {
                        circuitBreaker.ExecuteAsync(
                            ct => Task.FromResult(true), CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch { }
                    consecutiveFailures = 0;
                }
                else
                {
                    // Failure: execute action that throws
                    try
                    {
                        circuitBreaker.ExecuteAsync<bool>(
                            ct => throw new InvalidOperationException("simulated failure"),
                            CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (CircuitBreakerOpenException)
                    {
                        hasOpened = true;
                        break;
                    }
                    catch (InvalidOperationException)
                    {
                        // Expected - the action failed
                    }
                    consecutiveFailures++;
                }
            }

            // Assert: count how many consecutive failures appear in the sequence
            int maxConsecutiveFailures = GetMaxConsecutiveFailures(outcomes);

            if (maxConsecutiveFailures >= 5)
            {
                // Circuit must have opened
                hasOpened.Should().BeTrue(
                    "circuit breaker should open after 5 consecutive failures");
            }
            else
            {
                // Circuit should remain closed
                var finalState = circuitBreaker.GetState();
                finalState.State.Should().Be(CircuitState.Closed,
                    $"circuit breaker should remain closed with only {maxConsecutiveFailures} consecutive failures");
            }
        });
    }

    /// <summary>
    /// Property 9 (direct): Circuit breaker opens at exactly the 5th consecutive failure.
    /// For any number of preceding successes, after exactly 5 consecutive failures the state is Open.
    /// **Validates: Requirements 12.4**
    /// </summary>
    [Property]
    public FsCheck.Property CircuitBreakerTransitionsToOpenAtExactlyFifthFailure(PositiveInt prefixSuccesses)
    {
        return Prop.ForAll(Arb.Default.Bool(), _ =>
        {
            // Arrange
            var circuitBreaker = new CircuitBreakerService();
            int numSuccesses = prefixSuccesses.Get % 20; // Keep reasonable

            // Feed N successes first
            for (int i = 0; i < numSuccesses; i++)
            {
                circuitBreaker.ExecuteAsync(ct => Task.FromResult(true), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            // Verify still closed after successes
            circuitBreaker.GetState().State.Should().Be(CircuitState.Closed);

            // Feed exactly 4 failures — should remain closed
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    circuitBreaker.ExecuteAsync<bool>(
                        ct => throw new InvalidOperationException("failure"),
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (InvalidOperationException) { }
            }

            var stateAfterFour = circuitBreaker.GetState();
            stateAfterFour.State.Should().Be(CircuitState.Closed,
                "circuit should remain closed after only 4 consecutive failures");
            stateAfterFour.ConsecutiveFailures.Should().Be(4);

            // Feed the 5th failure — should transition to Open
            try
            {
                circuitBreaker.ExecuteAsync<bool>(
                    ct => throw new InvalidOperationException("failure"),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException) { }

            var stateAfterFive = circuitBreaker.GetState();
            stateAfterFive.State.Should().Be(CircuitState.Open,
                "circuit should open after exactly 5 consecutive failures");
        });
    }

    /// <summary>
    /// Property 9 (reset): A success resets the consecutive failure counter.
    /// Any sequence with fewer than 5 failures in a row keeps the circuit closed.
    /// **Validates: Requirements 12.4**
    /// </summary>
    [Property(Arbitrary = new[] { typeof(BoolSequenceArbitrary) })]
    public FsCheck.Property SuccessResetsConsecutiveFailureCounter(bool[] outcomes)
    {
        return Prop.ForAll(Arb.Default.Bool(), _ =>
        {
            // Only test sequences with fewer than 5 consecutive failures
            if (GetMaxConsecutiveFailures(outcomes) >= 5) return;

            var circuitBreaker = new CircuitBreakerService();

            foreach (var outcome in outcomes)
            {
                if (outcome)
                {
                    circuitBreaker.ExecuteAsync(ct => Task.FromResult(true), CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                else
                {
                    try
                    {
                        circuitBreaker.ExecuteAsync<bool>(
                            ct => throw new InvalidOperationException("failure"),
                            CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (InvalidOperationException) { }
                }
            }

            // Assert circuit remains closed
            circuitBreaker.GetState().State.Should().Be(CircuitState.Closed,
                "circuit should remain closed when consecutive failures never reach 5");
        });
    }

    /// <summary>
    /// Property 10: Circuit breaker rejects calls in open state without executing the action.
    /// After opening the circuit, any call is rejected with CircuitBreakerOpenException,
    /// and the provided action delegate is never invoked.
    /// **Validates: Requirements 12.5**
    /// </summary>
    [Property]
    public FsCheck.Property CircuitBreakerRejectsCallsInOpenState(PositiveInt additionalCalls)
    {
        return Prop.ForAll(Arb.Default.Bool(), _ =>
        {
            // Arrange: force circuit to open state with 5 consecutive failures
            var circuitBreaker = new CircuitBreakerService();
            ForceCircuitOpen(circuitBreaker);

            // Verify circuit is open
            circuitBreaker.GetState().State.Should().Be(CircuitState.Open);

            // Act & Assert: attempt additional calls (1 to 20)
            int callsToAttempt = (additionalCalls.Get % 20) + 1;
            int actionInvokedCount = 0;

            for (int i = 0; i < callsToAttempt; i++)
            {
                Action act = () => circuitBreaker.ExecuteAsync<bool>(ct =>
                {
                    Interlocked.Increment(ref actionInvokedCount);
                    return Task.FromResult(true);
                }, CancellationToken.None).GetAwaiter().GetResult();

                act.Should().Throw<CircuitBreakerOpenException>(
                    "all calls should be rejected when circuit is open");
            }

            // The action delegate should never have been invoked
            actionInvokedCount.Should().Be(0,
                "no network attempt should be made when circuit is open");
        });
    }

    /// <summary>
    /// Property 10 (void overload): Circuit breaker rejects void calls in open state.
    /// **Validates: Requirements 12.5**
    /// </summary>
    [Property]
    public FsCheck.Property CircuitBreakerRejectsVoidCallsInOpenState(PositiveInt additionalCalls)
    {
        return Prop.ForAll(Arb.Default.Bool(), _ =>
        {
            // Arrange: force circuit to open state
            var circuitBreaker = new CircuitBreakerService();
            ForceCircuitOpen(circuitBreaker);

            circuitBreaker.GetState().State.Should().Be(CircuitState.Open);

            // Act & Assert: attempt void calls
            int callsToAttempt = (additionalCalls.Get % 20) + 1;
            int actionInvokedCount = 0;

            for (int i = 0; i < callsToAttempt; i++)
            {
                Action act = () => circuitBreaker.ExecuteAsync(ct =>
                {
                    Interlocked.Increment(ref actionInvokedCount);
                    return Task.CompletedTask;
                }, CancellationToken.None).GetAwaiter().GetResult();

                act.Should().Throw<CircuitBreakerOpenException>(
                    "void calls should also be rejected when circuit is open");
            }

            actionInvokedCount.Should().Be(0,
                "no network attempt should be made for void calls when circuit is open");
        });
    }

    #region Helpers

    /// <summary>
    /// Forces the circuit breaker into Open state by causing 5 consecutive failures.
    /// </summary>
    private static void ForceCircuitOpen(CircuitBreakerService circuitBreaker)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                circuitBreaker.ExecuteAsync<bool>(
                    ct => throw new InvalidOperationException("force open"),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException) { }
        }
    }

    /// <summary>
    /// Calculates the maximum number of consecutive false values in a bool array.
    /// false represents a failure.
    /// </summary>
    private static int GetMaxConsecutiveFailures(bool[] outcomes)
    {
        int max = 0;
        int current = 0;
        foreach (var outcome in outcomes)
        {
            if (!outcome)
            {
                current++;
                if (current > max) max = current;
            }
            else
            {
                current = 0;
            }
        }
        return max;
    }

    #endregion
}

/// <summary>
/// Custom arbitrary for generating bool sequences of reasonable length (1-30 elements).
/// </summary>
public static class BoolSequenceArbitrary
{
    public static Arbitrary<bool[]> Generate()
    {
        var gen = Gen.ArrayOf(
            Gen.Choose(1, 30).SelectMany(size => Gen.ArrayOf(size, Arb.Default.Bool().Generator)));
        // Generate arrays of length 1-30
        var sized = Gen.Sized(s =>
        {
            var length = Gen.Choose(1, Math.Max(1, Math.Min(s, 30)));
            return length.SelectMany(len => Gen.ArrayOf(len, Arb.Default.Bool().Generator));
        });
        return Arb.From(sized, Arb.Default.Array<bool>().Shrinker);
    }
}
