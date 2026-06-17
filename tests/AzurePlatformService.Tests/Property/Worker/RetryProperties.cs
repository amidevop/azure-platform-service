using AzurePlatformService.Worker;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace AzurePlatformService.Tests.Property.Worker;

/// <summary>
/// Property 7: Exponential backoff retry timing.
/// Validates: Requirements 5.3, 12.1
/// </summary>
[Trait("Feature", "azure-platform-service")]
public class RetryProperties
{
    /// <summary>
    /// **Validates: Requirements 5.3, 12.1**
    /// Property 7: For attempt numbers 1-3, verify delay = baseDelay × multiplier^(N-1).
    /// With defaults: baseDelay=2s, multiplier=2 → delays: 2s, 4s, 8s.
    /// </summary>
    [Property]
    public FsCheck.Property ExponentialBackoff_DelayEquals_BaseDelay_Times_Multiplier_Power_AttemptMinusOne()
    {
        var attemptArb = Arb.From(Gen.Choose(1, 3));

        return Prop.ForAll(attemptArb, (int attempt) =>
        {
            // Arrange - use default values
            var baseDelay = TimeSpan.FromSeconds(2);
            var multiplier = 2.0;
            var policy = new ExponentialBackoffRetryPolicy(baseDelay, multiplier, maxRetries: 3);

            // Act
            var actualDelay = policy.ComputeDelay(attempt);

            // Assert - delay = baseDelay × multiplier^(attempt - 1)
            var expectedSeconds = baseDelay.TotalSeconds * Math.Pow(multiplier, attempt - 1);
            var expectedDelay = TimeSpan.FromSeconds(expectedSeconds);

            actualDelay.Should().Be(expectedDelay,
                because: $"delay for attempt {attempt} should be {baseDelay.TotalSeconds}s × {multiplier}^({attempt}-1) = {expectedSeconds}s");
        });
    }

    /// <summary>
    /// **Validates: Requirements 5.3, 12.1**
    /// Property 7 (extended): For arbitrary valid base delays and multipliers,
    /// the formula delay = baseDelay × multiplier^(N-1) holds.
    /// </summary>
    [Property]
    public FsCheck.Property ExponentialBackoff_ArbitraryParameters_FollowsFormula()
    {
        var arb = Arb.From(
            from baseDelayMs in Gen.Choose(100, 5000)
            from multiplierTenths in Gen.Choose(10, 40) // 1.0 to 4.0
            from attempt in Gen.Choose(1, 3)
            select (baseDelayMs, multiplierTenths, attempt));

        return Prop.ForAll(arb, ((int baseDelayMs, int multiplierTenths, int attempt) input) =>
        {
            var baseDelay = TimeSpan.FromMilliseconds(input.baseDelayMs);
            var multiplier = input.multiplierTenths / 10.0;
            var policy = new ExponentialBackoffRetryPolicy(baseDelay, multiplier, maxRetries: 3);

            // Act
            var actualDelay = policy.ComputeDelay(input.attempt);

            // Assert
            var expectedSeconds = baseDelay.TotalSeconds * Math.Pow(multiplier, input.attempt - 1);
            var expectedDelay = TimeSpan.FromSeconds(expectedSeconds);

            actualDelay.TotalMilliseconds.Should().BeApproximately(
                expectedDelay.TotalMilliseconds, 0.001,
                because: $"delay should follow baseDelay × multiplier^(attempt-1)");
        });
    }
}
