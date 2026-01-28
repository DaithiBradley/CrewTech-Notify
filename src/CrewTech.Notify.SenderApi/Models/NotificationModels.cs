namespace CrewTech.Notify.SenderApi.Models;

/// <summary>
/// Request model for sending a notification
/// </summary>
public class SendNotificationRequest
{
    /// <summary>
    /// Idempotency key to prevent duplicate sends
    /// </summary>
    public string? IdempotencyKey { get; set; }
    
    /// <summary>
    /// Target platform: WNS, FCM, iOS, Fake
    /// </summary>
    public string TargetPlatform { get; set; } = string.Empty;
    
    /// <summary>
    /// Device token or channel URI
    /// </summary>
    public string DeviceToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Notification title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Notification body/message
    /// </summary>
    public string Body { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional payload data
    /// </summary>
    public Dictionary<string, string>? Data { get; set; }
    
    /// <summary>
    /// Tags for filtering
    /// </summary>
    public List<string>? Tags { get; set; }
    
    /// <summary>
    /// Priority: Low, Normal, High
    /// </summary>
    public string Priority { get; set; } = "Normal";
    
    /// <summary>
    /// Scheduled delivery time (optional)
    /// </summary>
    public DateTime? ScheduledFor { get; set; }
}

/// <summary>
/// Response model for notification send
/// </summary>
public class SendNotificationResponse
{
    public Guid NotificationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response model for notification status query
/// </summary>
public class NotificationStatusResponse
{
    public Guid NotificationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TargetPlatform { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}
