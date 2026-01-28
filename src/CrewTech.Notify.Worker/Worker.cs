using CrewTech.Notify.Core.Entities;
using CrewTech.Notify.Core.Interfaces;
using CrewTech.Notify.Core.Services;
using CrewTech.Notify.Infrastructure.Providers;
using System.Text.Json;

namespace CrewTech.Notify.Worker;

/// <summary>
/// Background worker that processes pending notifications from the outbox
/// </summary>
public class NotificationWorker : BackgroundService
{
    private readonly ILogger<NotificationWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationProviderFactory _providerFactory;
    private readonly RetryPolicy _retryPolicy;
    private readonly int _batchSize = 10;
    private readonly int _pollingIntervalMs = 5000;

    public NotificationWorker(
        ILogger<NotificationWorker> logger,
        IServiceProvider serviceProvider,
        NotificationProviderFactory providerFactory)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _providerFactory = providerFactory;
        _retryPolicy = new RetryPolicy();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Worker started");
        _logger.LogInformation("Supported platforms: {Platforms}", 
            string.Join(", ", _providerFactory.GetSupportedPlatforms()));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingNotificationsAsync(stoppingToken);
                await ProcessFailedNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification worker loop");
            }

            await Task.Delay(_pollingIntervalMs, stoppingToken);
        }
        
        _logger.LogInformation("Notification Worker stopped");
    }

    private async Task ProcessPendingNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var pendingNotifications = await repository.GetPendingAsync(_batchSize, cancellationToken);
        var notifications = pendingNotifications.ToList();
        
        if (notifications.Any())
        {
            _logger.LogInformation("Processing {Count} pending notifications", notifications.Count);
        }

        foreach (var notification in notifications)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ProcessNotificationAsync(notification, repository, cancellationToken);
        }
    }

    private async Task ProcessFailedNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var failedNotifications = await repository.GetFailedForRetryAsync(_batchSize, cancellationToken);
        var notifications = failedNotifications.ToList();
        
        if (notifications.Any())
        {
            _logger.LogInformation("Retrying {Count} failed notifications", notifications.Count);
        }

        foreach (var notification in notifications)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Calculate delay for exponential backoff
            var delaySeconds = _retryPolicy.CalculateDelay(notification.RetryCount);
            var timeSinceLastUpdate = (DateTime.UtcNow - notification.UpdatedAt).TotalSeconds;
            
            if (timeSinceLastUpdate < delaySeconds)
            {
                // Not ready for retry yet
                continue;
            }

            await ProcessNotificationAsync(notification, repository, cancellationToken);
        }
    }

    private async Task ProcessNotificationAsync(
        NotificationMessage notification,
        INotificationRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            // Mark as processing
            await repository.MarkAsProcessingAsync(notification.Id, cancellationToken);
            
            _logger.LogInformation(
                "Processing notification {Id} for {Platform} (Attempt {Attempt}/{MaxRetries})",
                notification.Id, notification.TargetPlatform, 
                notification.RetryCount + 1, notification.MaxRetries);

            // Get provider for platform
            var provider = _providerFactory.GetProvider(notification.TargetPlatform);
            if (provider == null)
            {
                _logger.LogError("No provider found for platform {Platform}", notification.TargetPlatform);
                await repository.MoveToDeadLetterAsync(
                    notification.Id,
                    $"No provider registered for platform: {notification.TargetPlatform}",
                    cancellationToken);
                return;
            }

            // Parse data
            Dictionary<string, string>? data = null;
            if (!string.IsNullOrWhiteSpace(notification.Data))
            {
                try
                {
                    data = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.Data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize notification data");
                }
            }

            // Send notification
            var result = await provider.SendAsync(
                notification.DeviceToken,
                notification.Title,
                notification.Body,
                data,
                cancellationToken);

            if (result.Success)
            {
                // Success - mark as sent
                await repository.MarkAsSentAsync(notification.Id, cancellationToken);
                _logger.LogInformation("✓ Notification {Id} sent successfully", notification.Id);
            }
            else
            {
                // Failed - determine if we should retry or dead-letter
                if (!result.IsRetryable || notification.RetryCount >= notification.MaxRetries - 1)
                {
                    // Move to dead-letter
                    await repository.MoveToDeadLetterAsync(
                        notification.Id,
                        result.ErrorMessage ?? "Maximum retries exceeded",
                        cancellationToken);
                    
                    _logger.LogWarning(
                        "✗ Notification {Id} moved to dead-letter: {Error}",
                        notification.Id, result.ErrorMessage);
                }
                else
                {
                    // Mark as failed for retry
                    await repository.MarkAsFailedAsync(
                        notification.Id,
                        result.ErrorMessage ?? "Unknown error",
                        deadLetter: false,
                        cancellationToken);
                    
                    var nextDelay = _retryPolicy.CalculateDelay(notification.RetryCount + 1);
                    _logger.LogWarning(
                        "⚠ Notification {Id} failed, will retry in ~{Delay}s: {Error}",
                        notification.Id, nextDelay, result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception processing notification {Id}", notification.Id);
            
            // Mark as failed
            if (notification.RetryCount >= notification.MaxRetries - 1)
            {
                await repository.MoveToDeadLetterAsync(
                    notification.Id,
                    $"Exception: {ex.Message}",
                    cancellationToken);
            }
            else
            {
                await repository.MarkAsFailedAsync(
                    notification.Id,
                    ex.Message,
                    deadLetter: false,
                    cancellationToken);
            }
        }
    }
}

