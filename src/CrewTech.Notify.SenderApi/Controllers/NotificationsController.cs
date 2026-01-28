using CrewTech.Notify.Core.Entities;
using CrewTech.Notify.Core.Interfaces;
using CrewTech.Notify.SenderApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CrewTech.Notify.SenderApi.Controllers;

/// <summary>
/// API controller for sending notifications
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationsController> _logger;
    
    public NotificationsController(
        INotificationRepository repository,
        ILogger<NotificationsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    /// <summary>
    /// Send a new notification
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SendNotificationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.TargetPlatform) ||
            string.IsNullOrWhiteSpace(request.DeviceToken) ||
            string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { Error = "TargetPlatform, DeviceToken, and Title are required" });
        }
        
        // Generate idempotency key if not provided
        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();
        
        // Check for duplicate using idempotency key
        var existing = await _repository.GetByIdempotencyKeyAsync(idempotencyKey);
        if (existing != null)
        {
            _logger.LogInformation("Duplicate notification request with idempotency key {Key}", idempotencyKey);
            return Conflict(new SendNotificationResponse
            {
                NotificationId = existing.Id,
                Status = existing.Status.ToString(),
                Message = "Notification with this idempotency key already exists"
            });
        }
        
        // Create notification message in outbox
        var notification = new NotificationMessage
        {
            IdempotencyKey = idempotencyKey,
            TargetPlatform = request.TargetPlatform,
            DeviceToken = request.DeviceToken,
            Title = request.Title,
            Body = request.Body,
            Data = request.Data != null ? JsonSerializer.Serialize(request.Data) : null,
            Tags = request.Tags != null ? string.Join(",", request.Tags) : string.Empty,
            Priority = request.Priority,
            ScheduledFor = request.ScheduledFor,
            Status = NotificationStatus.Pending
        };
        
        await _repository.AddAsync(notification);
        
        _logger.LogInformation(
            "Notification {Id} queued for {Platform} to {DeviceToken}",
            notification.Id, notification.TargetPlatform, notification.DeviceToken);
        
        return Accepted(new SendNotificationResponse
        {
            NotificationId = notification.Id,
            Status = "Accepted",
            Message = "Notification queued for delivery"
        });
    }
    
    /// <summary>
    /// Get notification status by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NotificationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotificationStatus(Guid id)
    {
        var notification = await _repository.GetByIdAsync(id);
        if (notification == null)
        {
            return NotFound(new { Error = "Notification not found" });
        }
        
        return Ok(new NotificationStatusResponse
        {
            NotificationId = notification.Id,
            Status = notification.Status.ToString(),
            TargetPlatform = notification.TargetPlatform,
            RetryCount = notification.RetryCount,
            CreatedAt = notification.CreatedAt,
            SentAt = notification.SentAt,
            ErrorMessage = notification.ErrorMessage
        });
    }
    
    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("/health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}
