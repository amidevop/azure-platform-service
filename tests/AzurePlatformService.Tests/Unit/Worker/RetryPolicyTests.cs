using AzurePlatformService.Worker;
using FluentAssertions;
using Xunit;

namespace AzurePlatformService.Tests.Unit.Worker;

public class RetryPolicyTests
{
    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        var policy = new ExponentialBackoffRetryPolicy();
        policy.MaxRetries.Should().Be(3);
    }

    [Theory]
    [InlineData(1, 2.0)]   // 2 * 2^0 = 2
    [InlineData(2, 4.0)]   // 2 * 2^1 = 4
    [InlineData(3, 8.0)]   // 2 * 2^2 = 8
    public void ComputeDelay_ReturnsCorrectExponentialBackoff(int attempt, double expectedSeconds)
    {
        var policy = new ExponentialBackoffRetryPolicy(
            baseDelay: TimeSpan.FromSeconds(2),
            multiplier: 2.0,
            maxRetries: 3);

        var delay = policy.ComputeDelay(attempt);

        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void ComputeDelay_WithCustomParameters_ComputesCorrectly()
    {
        var policy = new ExponentialBackoffRetryPolicy(
            baseDelay: TimeSpan.FromSeconds(1),
            multiplier: 3.0,
            maxRetries: 5);

        policy.ComputeDelay(1).Should().Be(TimeSpan.FromSeconds(1));   // 1 * 3^0 = 1
        policy.ComputeDelay(2).Should().Be(TimeSpan.FromSeconds(3));   // 1 * 3^1 = 3
        policy.ComputeDelay(3).Should().Be(TimeSpan.FromSeconds(9));   // 1 * 3^2 = 9
    }

    [Fact]
    public void ComputeDelay_WithAttemptLessThan1_ThrowsArgumentOutOfRange()
    {
        var policy = new ExponentialBackoffRetryPolicy();

        var act = () => policy.ComputeDelay(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_CustomMaxRetries_SetsCorrectly()
    {
        var policy = new ExponentialBackoffRetryPolicy(maxRetries: 5);
        policy.MaxRetries.Should().Be(5);
    }
}
