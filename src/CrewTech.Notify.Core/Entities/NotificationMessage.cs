namespace CrewTech.Notify.Core.Entities;

/// <summary>
/// Represents a notification message stored in the outbox pattern for reliable delivery
/// </summary>
public class NotificationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Idempotency key to prevent duplicate processing
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Target platform: WNS, FCM, iOS, Slack, etc.
    /// </summary>
    public string TargetPlatform { get; set; } = string.Empty;
    
    /// <summary>
    /// Device token or channel URI for push notifications
    /// </summary>
    public string DeviceToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Notification title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Notification body/message content
    /// </summary>
    public string Body { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional payload data as JSON
    /// </summary>
    public string? Data { get; set; }
    
    /// <summary>
    /// Tags for filtering and categorization
    /// </summary>
    public string Tags { get; set; } = string.Empty;
    
    /// <summary>
    /// Priority level: Low, Normal, High
    /// </summary>
    public string Priority { get; set; } = "Normal";
    
    /// <summary>
    /// Current status: Pending, Processing, Sent, Failed, DeadLettered
    /// </summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    
    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Maximum number of retries allowed
    /// </summary>
    public int MaxRetries { get; set; } = 5;
    
    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the message was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the message should be processed (for scheduled delivery)
    /// </summary>
    public DateTime? ScheduledFor { get; set; }
    
    /// <summary>
    /// When the message was successfully sent
    /// </summary>
    public DateTime? SentAt { get; set; }
    
    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Last error details
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Notification status enumeration
/// </summary>
public enum NotificationStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    DeadLettered = 4
}
