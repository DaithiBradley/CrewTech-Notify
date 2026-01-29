using CrewTech.Notify.Core.Entities;
using CrewTech.Notify.Core.Interfaces;
using CrewTech.Notify.Core.Services;
using CrewTech.Notify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrewTech.Notify.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for NotificationMessage using EF Core
/// </summary>
public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;
    
    public NotificationRepository(NotificationDbContext context)
    {
        _context = context;
    }
    
    public async Task<NotificationMessage> AddAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _context.NotificationMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }
    
    public async Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationMessages
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }
    
    public async Task<NotificationMessage?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationMessages
            .FirstOrDefaultAsync(m => m.IdempotencyKey == idempotencyKey, cancellationToken);
    }
    
    public async Task<IEnumerable<NotificationMessage>> GetPendingAsync(int batchSize = 10, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.NotificationMessages
            .Where(m => m.Status == NotificationStatus.Pending &&
                       (m.ScheduledFor == null || m.ScheduledFor <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IEnumerable<NotificationMessage>> GetFailedForRetryAsync(int batchSize = 10, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.NotificationMessages
            .Where(m => m.Status == NotificationStatus.Failed &&
                       m.RetryCount < m.MaxRetries &&
                       (m.NextAttemptUtc == null || m.NextAttemptUtc <= now))
            .OrderBy(m => m.NextAttemptUtc ?? m.UpdatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
    
    public async Task UpdateAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        message.UpdatedAt = DateTime.UtcNow;
        _context.NotificationMessages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }
    
    public async Task MarkAsProcessingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await GetByIdAsync(id, cancellationToken);
        if (message != null)
        {
            message.Status = NotificationStatus.Processing;
            message.LastAttemptUtc = DateTime.UtcNow;
            message.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
    
    public async Task MarkAsSentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await GetByIdAsync(id, cancellationToken);
        if (message != null)
        {
            message.Status = NotificationStatus.Sent;
            message.SentAt = DateTime.UtcNow;
            message.UpdatedAt = DateTime.UtcNow;
            message.ErrorMessage = null;
            message.LastError = null;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
    
    public async Task MarkAsFailedAsync(Guid id, string errorMessage, bool deadLetter = false, string? errorCategory = null, CancellationToken cancellationToken = default)
    {
        var message = await GetByIdAsync(id, cancellationToken);
        if (message != null)
        {
            message.Status = deadLetter ? NotificationStatus.DeadLettered : NotificationStatus.Failed;
            message.RetryCount++;
            message.LastError = errorMessage;
            message.ErrorMessage = errorMessage;
            message.LastAttemptUtc = DateTime.UtcNow;
            message.LastErrorCategory = errorCategory;
            message.UpdatedAt = DateTime.UtcNow;
            
            // Calculate next attempt time using exponential backoff
            if (!deadLetter)
            {
                var retryPolicy = new RetryPolicy();
                var delaySeconds = retryPolicy.CalculateDelay(message.RetryCount);
                message.NextAttemptUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
            }
            
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
    
    public async Task MoveToDeadLetterAsync(Guid id, string reason, string? errorCategory = null, CancellationToken cancellationToken = default)
    {
        var message = await GetByIdAsync(id, cancellationToken);
        if (message != null)
        {
            message.Status = NotificationStatus.DeadLettered;
            message.ErrorMessage = reason;
            message.LastErrorCategory = errorCategory;
            message.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
