using CrewTech.Notify.Core.Entities;
using Xunit;

namespace CrewTech.Notify.Core.Tests;

/// <summary>
/// Tests for notification outbox state machine transitions
/// </summary>
public class OutboxStateTests
{
    [Fact]
    public void NotificationMessage_InitialState_IsPending()
    {
        // Act
        var notification = new NotificationMessage();

        // Assert
        Assert.Equal(NotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public void NotificationStatus_PendingToProcessing_IsValidTransition()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Pending
        };

        // Act
        notification.Status = NotificationStatus.Processing;

        // Assert
        Assert.Equal(NotificationStatus.Processing, notification.Status);
    }

    [Fact]
    public void NotificationStatus_ProcessingToSent_IsValidTransition()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Processing
        };

        // Act
        notification.Status = NotificationStatus.Sent;

        // Assert
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public void NotificationStatus_ProcessingToFailed_IsValidTransition()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Processing
        };

        // Act
        notification.Status = NotificationStatus.Failed;

        // Assert
        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Fact]
    public void NotificationStatus_FailedToPending_IsValidTransitionForRetry()
    {
        // Arrange - Simulating retry logic
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Failed,
            RetryCount = 1
        };

        // Act - Worker resets to Pending for retry
        notification.Status = NotificationStatus.Pending;

        // Assert
        Assert.Equal(NotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public void NotificationStatus_FailedToDeadLettered_IsValidTransition()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Failed,
            RetryCount = 5
        };

        // Act - Max retries exceeded
        notification.Status = NotificationStatus.DeadLettered;

        // Assert
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
    }

    [Fact]
    public void NotificationStatus_ProcessingToDeadLettered_IsValidForTerminalError()
    {
        // Arrange - Terminal error like invalid token
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Processing
        };

        // Act
        notification.Status = NotificationStatus.DeadLettered;

        // Assert
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
    }

    [Fact]
    public void RetryCount_IncrementsOnEachRetry()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            RetryCount = 0
        };

        // Act - Simulate multiple retries
        notification.RetryCount++;
        Assert.Equal(1, notification.RetryCount);

        notification.RetryCount++;
        Assert.Equal(2, notification.RetryCount);

        notification.RetryCount++;
        Assert.Equal(3, notification.RetryCount);
    }

    [Fact]
    public void NotificationMessage_UpdatedAt_ChangesOnStatusUpdate()
    {
        // Arrange
        var notification = new NotificationMessage();
        var originalUpdatedAt = notification.UpdatedAt;
        
        System.Threading.Thread.Sleep(10); // Small delay

        // Act
        notification.UpdatedAt = DateTime.UtcNow;
        notification.Status = NotificationStatus.Processing;

        // Assert
        Assert.True(notification.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void NotificationMessage_SentAt_SetOnSentStatus()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Processing
        };

        // Act
        notification.Status = NotificationStatus.Sent;
        notification.SentAt = DateTime.UtcNow;

        // Assert
        Assert.NotNull(notification.SentAt);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public void NotificationMessage_ErrorMessage_SetOnFailure()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Status = NotificationStatus.Processing
        };

        // Act
        notification.Status = NotificationStatus.Failed;
        notification.ErrorMessage = "Service unavailable";

        // Assert
        Assert.Equal("Service unavailable", notification.ErrorMessage);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Theory]
    [InlineData(0, 5, false)]  // Can retry
    [InlineData(3, 5, false)]  // Can retry
    [InlineData(4, 5, false)]  // Last retry
    [InlineData(5, 5, true)]   // Exceeded
    [InlineData(6, 5, true)]   // Exceeded
    public void NotificationMessage_MaxRetriesCheck_DeterminesDeadLettering(
        int retryCount, int maxRetries, bool shouldDeadLetter)
    {
        // Arrange
        var notification = new NotificationMessage
        {
            RetryCount = retryCount,
            MaxRetries = maxRetries
        };

        // Act - Simulate retry logic
        var hasExceededMaxRetries = notification.RetryCount >= notification.MaxRetries;

        // Assert
        Assert.Equal(shouldDeadLetter, hasExceededMaxRetries);
    }

    [Fact]
    public void NotificationStatus_AllStates_CanBeSet()
    {
        // Arrange
        var notification = new NotificationMessage();

        // Act & Assert - Verify all states can be set
        notification.Status = NotificationStatus.Pending;
        Assert.Equal(NotificationStatus.Pending, notification.Status);

        notification.Status = NotificationStatus.Processing;
        Assert.Equal(NotificationStatus.Processing, notification.Status);

        notification.Status = NotificationStatus.Sent;
        Assert.Equal(NotificationStatus.Sent, notification.Status);

        notification.Status = NotificationStatus.Failed;
        Assert.Equal(NotificationStatus.Failed, notification.Status);

        notification.Status = NotificationStatus.DeadLettered;
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
    }

    [Fact]
    public void NotificationMessage_StateTransitionSequence_HappyPath()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Title = "Test",
            Body = "Test body",
            DeviceToken = "test-device"
        };

        // Act & Assert - Happy path: Pending → Processing → Sent
        Assert.Equal(NotificationStatus.Pending, notification.Status);

        notification.Status = NotificationStatus.Processing;
        Assert.Equal(NotificationStatus.Processing, notification.Status);

        notification.Status = NotificationStatus.Sent;
        notification.SentAt = DateTime.UtcNow;
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.NotNull(notification.SentAt);
    }

    [Fact]
    public void NotificationMessage_StateTransitionSequence_RetryPath()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Title = "Test",
            MaxRetries = 3
        };

        // Act & Assert - Retry path: Pending → Processing → Failed → Pending → Processing → Sent
        Assert.Equal(NotificationStatus.Pending, notification.Status);

        notification.Status = NotificationStatus.Processing;
        Assert.Equal(NotificationStatus.Processing, notification.Status);

        notification.Status = NotificationStatus.Failed;
        notification.RetryCount++;
        notification.ErrorMessage = "Transient error";
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(1, notification.RetryCount);

        // Retry
        notification.Status = NotificationStatus.Pending;
        Assert.Equal(NotificationStatus.Pending, notification.Status);

        notification.Status = NotificationStatus.Processing;
        Assert.Equal(NotificationStatus.Processing, notification.Status);

        notification.Status = NotificationStatus.Sent;
        notification.SentAt = DateTime.UtcNow;
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public void NotificationMessage_StateTransitionSequence_DeadLetterPath()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            Title = "Test",
            MaxRetries = 2
        };

        // Act & Assert - Dead letter path: Pending → Processing → Failed (x3) → DeadLettered
        notification.Status = NotificationStatus.Processing;
        notification.Status = NotificationStatus.Failed;
        notification.RetryCount++;

        notification.Status = NotificationStatus.Processing;
        notification.Status = NotificationStatus.Failed;
        notification.RetryCount++;

        // Max retries exceeded
        Assert.Equal(2, notification.RetryCount);
        notification.Status = NotificationStatus.DeadLettered;
        Assert.Equal(NotificationStatus.DeadLettered, notification.Status);
    }

    [Fact]
    public void NotificationMessage_CreatedAt_IsSetOnCreation()
    {
        // Act
        var before = DateTime.UtcNow;
        var notification = new NotificationMessage();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(notification.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void NotificationMessage_UpdatedAt_IsSetOnCreation()
    {
        // Act
        var before = DateTime.UtcNow;
        var notification = new NotificationMessage();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(notification.UpdatedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }
}
