using CrewTech.Notify.Core.Services;
using Xunit;

namespace CrewTech.Notify.Core.Tests;

public class RetryPolicyTests
{
    [Fact]
    public void CalculateDelay_ReturnsExponentialBackoff()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            BaseDelaySeconds = 5,
            MaxDelaySeconds = 300,
            JitterFactor = 0
        };

        // Act & Assert
        var delay0 = policy.CalculateDelay(0);
        var delay1 = policy.CalculateDelay(1);
        var delay2 = policy.CalculateDelay(2);

        // With no jitter: delay = baseDelay * 2^retryCount
        Assert.Equal(5, delay0);    // 5 * 2^0 = 5
        Assert.Equal(10, delay1);   // 5 * 2^1 = 10
        Assert.Equal(20, delay2);   // 5 * 2^2 = 20
    }

    [Fact]
    public void CalculateDelay_CapsAtMaxDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            BaseDelaySeconds = 10,
            MaxDelaySeconds = 60,
            JitterFactor = 0
        };

        // Act
        var delay10 = policy.CalculateDelay(10);

        // Assert - should be capped at max
        Assert.Equal(60, delay10);
    }

    [Fact]
    public void CalculateDelay_WithJitter_VariesResults()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            BaseDelaySeconds = 10,
            MaxDelaySeconds = 300,
            JitterFactor = 0.3
        };

        // Act - run multiple times to verify jitter
        var delays = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            delays.Add(policy.CalculateDelay(2));
        }

        // Assert - delays should vary due to jitter
        // With retry 2: base = 10 * 2^2 = 40
        // Jitter range: 40 +/- (40 * 0.3 * random) = roughly 34-46
        Assert.All(delays, d => Assert.InRange(d, 30, 50));
        
        // At least some variation (not all the same)
        var uniqueDelays = delays.Distinct().Count();
        Assert.True(uniqueDelays > 1, "Jitter should produce varied delays");
    }

    [Fact]
    public void ShouldRetry_ReturnsTrueWhenBelowMax()
    {
        // Arrange
        var policy = new RetryPolicy();

        // Act & Assert
        Assert.True(policy.ShouldRetry(0, 5));
        Assert.True(policy.ShouldRetry(2, 5));
        Assert.True(policy.ShouldRetry(4, 5));
    }

    [Fact]
    public void ShouldRetry_ReturnsFalseWhenAtOrAboveMax()
    {
        // Arrange
        var policy = new RetryPolicy();

        // Act & Assert
        Assert.False(policy.ShouldRetry(5, 5));
        Assert.False(policy.ShouldRetry(6, 5));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    public void CalculateDelay_Theory_ReturnsExpectedValues(int retryCount, int expectedMin)
    {
        // Arrange
        var policy = new RetryPolicy
        {
            BaseDelaySeconds = 5,
            MaxDelaySeconds = 300,
            JitterFactor = 0
        };

        // Act
        var delay = policy.CalculateDelay(retryCount);

        // Assert
        Assert.Equal(expectedMin, delay);
    }
}
