using CrewTech.Notify.Core.Entities;
using Xunit;

namespace CrewTech.Notify.Core.Tests;

public class NotificationMessageTests
{
    [Fact]
    public void NotificationMessage_DefaultValues_AreSet()
    {
        // Act
        var notification = new NotificationMessage();

        // Assert
        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(0, notification.RetryCount);
        Assert.Equal(5, notification.MaxRetries);
        Assert.Equal("Normal", notification.Priority);
        Assert.True((DateTime.UtcNow - notification.CreatedAt).TotalSeconds < 1);
    }

    [Fact]
    public void NotificationMessage_CanSetProperties()
    {
        // Arrange
        var notification = new NotificationMessage();
        var testId = Guid.NewGuid();

        // Act
        notification.Id = testId;
        notification.IdempotencyKey = "test-key-123";
        notification.TargetPlatform = "WNS";
        notification.DeviceToken = "device-token-abc";
        notification.Title = "Test Title";
        notification.Body = "Test Body";
        notification.Tags = "tag1,tag2";
        notification.Status = NotificationStatus.Sent;

        // Assert
        Assert.Equal(testId, notification.Id);
        Assert.Equal("test-key-123", notification.IdempotencyKey);
        Assert.Equal("WNS", notification.TargetPlatform);
        Assert.Equal("device-token-abc", notification.DeviceToken);
        Assert.Equal("Test Title", notification.Title);
        Assert.Equal("Test Body", notification.Body);
        Assert.Equal("tag1,tag2", notification.Tags);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Theory]
    [InlineData(NotificationStatus.Pending)]
    [InlineData(NotificationStatus.Processing)]
    [InlineData(NotificationStatus.Sent)]
    [InlineData(NotificationStatus.Failed)]
    [InlineData(NotificationStatus.DeadLettered)]
    public void NotificationMessage_CanSetAllStatuses(NotificationStatus status)
    {
        // Arrange
        var notification = new NotificationMessage();

        // Act
        notification.Status = status;

        // Assert
        Assert.Equal(status, notification.Status);
    }
}
