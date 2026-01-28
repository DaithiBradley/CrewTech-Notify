namespace CrewTech.Notify.Core.Interfaces;

/// <summary>
/// Interface for notification provider implementations
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// Gets the platform this provider supports (e.g., "WNS", "FCM", "iOS", "Fake")
    /// </summary>
    string Platform { get; }
    
    /// <summary>
    /// Sends a notification through the provider
    /// </summary>
    /// <param name="deviceToken">Device token or channel URI</param>
    /// <param name="title">Notification title</param>
    /// <param name="body">Notification body</param>
    /// <param name="data">Additional payload data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a notification send operation
/// </summary>
public class NotificationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public bool IsRetryable { get; init; }
    public FailureCategory Category { get; init; }
    
    public static NotificationResult Ok() => new() { Success = true };
    
    public static NotificationResult Fail(
        string errorMessage,
        bool isRetryable = true,
        string? errorCode = null,
        FailureCategory category = FailureCategory.Unknown) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            IsRetryable = isRetryable,
            Category = category
        };
}

/// <summary>
/// Classification of notification failures for retry logic
/// </summary>
public enum FailureCategory
{
    Unknown,
    NetworkError,          // Retryable
    ServiceUnavailable,    // Retryable
    RateLimited,           // Retryable
    InvalidToken,          // Terminal
    InvalidPayload,        // Terminal
    Unauthorized,          // Terminal
    PlatformNotSupported   // Terminal
}
