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
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public bool IsRetryable { get; set; }
    
    public static NotificationResult Ok() => new() { Success = true };
    
    public static NotificationResult Fail(string errorMessage, bool isRetryable = true, string? errorCode = null) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            IsRetryable = isRetryable
        };
}
