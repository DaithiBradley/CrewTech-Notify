using CrewTech.Notify.Core.Entities;
using CrewTech.Notify.Infrastructure.Data;
using CrewTech.Notify.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrewTech.Notify.Infrastructure.Tests;

public class NotificationRepositoryTests
{
    private NotificationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new NotificationDbContext(options);
    }

    [Fact]
    public async Task AddAsync_AddsNotificationToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NotificationRepository(context);
        var notification = new NotificationMessage
        {
            IdempotencyKey = "test-key",
            TargetPlatform = "Fake",
            DeviceToken = "device-123",
            Title = "Test",
            Body = "Test message"
        };

        // Act
        var result = await repository.AddAsync(notification);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        var saved = await context.NotificationMessages.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal("test-key", saved.IdempotencyKey);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_ReturnsExistingNotification()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NotificationRepository(context);
        var notification = new NotificationMessage
        {
            IdempotencyKey = "unique-key-123",
            TargetPlatform = "Fake",
            DeviceToken = "device-123",
            Title = "Test",
            Body = "Test message"
        };
        await repository.AddAsync(notification);

        // Act
        var result = await repository.GetByIdempotencyKeyAsync("unique-key-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unique-key-123", result.IdempotencyKey);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingNotifications()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NotificationRepository(context);
        
        // Add pending notification
        await repository.AddAsync(new NotificationMessage
        {
            IdempotencyKey = "pending-1",
            TargetPlatform = "Fake",
            DeviceToken = "device-1",
            Title = "Pending",
            Body = "Pending message",
            Status = NotificationStatus.Pending
        });
        
        // Add sent notification (should not be returned)
        await repository.AddAsync(new NotificationMessage
        {
            IdempotencyKey = "sent-1",
            TargetPlatform = "Fake",
            DeviceToken = "device-2",
            Title = "Sent",
            Body = "Sent message",
            Status = NotificationStatus.Sent
        });

        // Act
        var pending = await repository.GetPendingAsync(10);

        // Assert
        var pendingList = pending.ToList();
        Assert.Single(pendingList);
        Assert.Equal("pending-1", pendingList[0].IdempotencyKey);
    }

    [Fact]
    public async Task MarkAsSentAsync_UpdatesStatusAndSentAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NotificationRepository(context);
        var notification = new NotificationMessage
        {
            IdempotencyKey = "test-key",
            TargetPlatform = "Fake",
            DeviceToken = "device-123",
            Title = "Test",
            Body = "Test message"
        };
        await repository.AddAsync(notification);

        // Act
        await repository.MarkAsSentAsync(notification.Id);

        // Assert
        var updated = await repository.GetByIdAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal(NotificationStatus.Sent, updated.Status);
        Assert.NotNull(updated.SentAt);
    }

    [Fact]
    public async Task MarkAsFailedAsync_IncrementsRetryCount()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NotificationRepository(context);
        var notification = new NotificationMessage
        {
            IdempotencyKey = "test-key",
            TargetPlatform = "Fake",
            DeviceToken = "device-123",
            Title = "Test",
            Body = "Test message"
        };
        await repository.AddAsync(notification);
        var initialRetryCount = notification.RetryCount;

        // Act
        await repository.MarkAsFailedAsync(notification.Id, "Test error");

        // Assert
        var updated = await repository.GetByIdAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal(NotificationStatus.Failed, updated.Status);
        Assert.Equal(initialRetryCount + 1, updated.RetryCount);
        Assert.Equal("Test error", updated.ErrorMessage);
    }

    [Fact]
    public async Task MoveToDeadLetterAsync_SetsDeadLetteredStatus()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NotificationRepository(context);
        var notification = new NotificationMessage
        {
            IdempotencyKey = "test-key",
            TargetPlatform = "Fake",
            DeviceToken = "device-123",
            Title = "Test",
            Body = "Test message"
        };
        await repository.AddAsync(notification);

        // Act
        await repository.MoveToDeadLetterAsync(notification.Id, "Max retries exceeded");

        // Assert
        var updated = await repository.GetByIdAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal(NotificationStatus.DeadLettered, updated.Status);
        Assert.Contains("Max retries exceeded", updated.ErrorMessage);
    }
}
