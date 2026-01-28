namespace CrewTech.Notify.Core.Services;

/// <summary>
/// Retry policy with exponential backoff and jitter
/// </summary>
public class RetryPolicy
{
    private readonly Random _random = new();
    
    /// <summary>
    /// Base delay in seconds for retry calculation
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 5;
    
    /// <summary>
    /// Maximum delay in seconds
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 300; // 5 minutes
    
    /// <summary>
    /// Jitter factor (0.0 to 1.0)
    /// </summary>
    public double JitterFactor { get; set; } = 0.3;
    
    /// <summary>
    /// Calculates the delay before the next retry with exponential backoff and jitter
    /// </summary>
    /// <param name="retryCount">Current retry count</param>
    /// <returns>Delay in seconds</returns>
    public int CalculateDelay(int retryCount)
    {
        // Exponential backoff: delay = baseDelay * 2^retryCount
        var exponentialDelay = BaseDelaySeconds * Math.Pow(2, retryCount);
        
        // Cap at max delay
        exponentialDelay = Math.Min(exponentialDelay, MaxDelaySeconds);
        
        // Add jitter to prevent thundering herd
        var jitter = exponentialDelay * JitterFactor * (_random.NextDouble() - 0.5);
        var finalDelay = exponentialDelay + jitter;
        
        return (int)Math.Max(1, finalDelay);
    }
    
    /// <summary>
    /// Determines if a retry should be attempted
    /// </summary>
    /// <param name="retryCount">Current retry count</param>
    /// <param name="maxRetries">Maximum allowed retries</param>
    /// <returns>True if retry should be attempted</returns>
    public bool ShouldRetry(int retryCount, int maxRetries)
    {
        return retryCount < maxRetries;
    }
}
