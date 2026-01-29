using CrewTech.Notify.Core.Entities;

namespace CrewTech.Notify.Core.Interfaces;

/// <summary>
/// Repository interface for notification messages (Outbox pattern)
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Adds a new notification message to the outbox
    /// </summary>
    Task<NotificationMessage> AddAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a notification message by ID
    /// </summary>
    Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a notification message by idempotency key
    /// </summary>
    Task<NotificationMessage?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets pending notifications ready for processing
    /// </summary>
    Task<IEnumerable<NotificationMessage>> GetPendingAsync(int batchSize = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets failed notifications eligible for retry
    /// </summary>
    Task<IEnumerable<NotificationMessage>> GetFailedForRetryAsync(int batchSize = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a notification message
    /// </summary>
    Task UpdateAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks a notification as processing
    /// </summary>
    Task MarkAsProcessingAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks a notification as sent
    /// </summary>
    Task MarkAsSentAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks a notification as failed
    /// </summary>
    Task MarkAsFailedAsync(Guid id, string errorMessage, bool deadLetter = false, string? errorCategory = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves a notification to dead-letter queue
    /// </summary>
    Task MoveToDeadLetterAsync(Guid id, string reason, string? errorCategory = null, CancellationToken cancellationToken = default);
}
