using CrewTech.Notify.Core.Entities;
using Xunit;

namespace CrewTech.Notify.Core.Tests;

/// <summary>
/// Tests for idempotency key handling and duplicate detection
/// </summary>
public class IdempotencyTests
{
    [Fact]
    public void IdempotencyKey_SameKey_ShouldBeDetected()
    {
        // Arrange
        var key = "unique-key-123";
        var notification1 = new NotificationMessage { IdempotencyKey = key };
        var notification2 = new NotificationMessage { IdempotencyKey = key };

        // Assert - Same idempotency keys should match
        Assert.Equal(notification1.IdempotencyKey, notification2.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_DifferentKeys_ShouldBeDifferent()
    {
        // Arrange
        var notification1 = new NotificationMessage { IdempotencyKey = "key-1" };
        var notification2 = new NotificationMessage { IdempotencyKey = "key-2" };

        // Assert
        Assert.NotEqual(notification1.IdempotencyKey, notification2.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_EmptyString_CanBeSet()
    {
        // Act
        var notification = new NotificationMessage { IdempotencyKey = string.Empty };

        // Assert
        Assert.Equal(string.Empty, notification.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_GuidFormat_IsValid()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        // Act
        var notification = new NotificationMessage { IdempotencyKey = guid };

        // Assert
        Assert.Equal(guid, notification.IdempotencyKey);
        Assert.True(Guid.TryParse(notification.IdempotencyKey, out _));
    }

    [Theory]
    [InlineData("order-12345")]
    [InlineData("user-action-67890")]
    [InlineData("payment-xyz-abc")]
    public void IdempotencyKey_CustomFormats_CanBeUsed(string key)
    {
        // Act
        var notification = new NotificationMessage { IdempotencyKey = key };

        // Assert
        Assert.Equal(key, notification.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_CaseSensitive_AreDifferent()
    {
        // Arrange
        var notification1 = new NotificationMessage { IdempotencyKey = "KEY-123" };
        var notification2 = new NotificationMessage { IdempotencyKey = "key-123" };

        // Assert - Idempotency keys should be case-sensitive
        Assert.NotEqual(notification1.IdempotencyKey, notification2.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_WithSpecialCharacters_CanBeStored()
    {
        // Arrange
        var specialKeys = new[]
        {
            "key-with-dashes",
            "key_with_underscores",
            "key.with.dots",
            "key:with:colons"
        };

        foreach (var key in specialKeys)
        {
            // Act
            var notification = new NotificationMessage { IdempotencyKey = key };

            // Assert
            Assert.Equal(key, notification.IdempotencyKey);
        }
    }

    [Fact]
    public void NotificationMessage_TwoWithSameKey_HaveSameIdempotencyKey()
    {
        // Arrange
        var sharedKey = "shared-key-999";
        
        var notification1 = new NotificationMessage 
        { 
            IdempotencyKey = sharedKey,
            Title = "First notification"
        };
        
        var notification2 = new NotificationMessage 
        { 
            IdempotencyKey = sharedKey,
            Title = "Second notification (duplicate)"
        };

        // Assert - Should have same idempotency key but different IDs
        Assert.Equal(notification1.IdempotencyKey, notification2.IdempotencyKey);
        Assert.NotEqual(notification1.Id, notification2.Id); // Different Guids
    }

    [Fact]
    public void IdempotencyKey_LongKey_CanBeStored()
    {
        // Arrange - Test very long idempotency keys
        var longKey = new string('X', 500);

        // Act
        var notification = new NotificationMessage { IdempotencyKey = longKey };

        // Assert
        Assert.Equal(longKey, notification.IdempotencyKey);
        Assert.Equal(500, notification.IdempotencyKey.Length);
    }

    [Fact]
    public void NotificationMessage_DefaultIdempotencyKey_IsEmptyString()
    {
        // Act
        var notification = new NotificationMessage();

        // Assert
        Assert.Equal(string.Empty, notification.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_AcrossDifferentStates_RemainsConstant()
    {
        // Arrange
        var key = "consistent-key";
        var notification = new NotificationMessage { IdempotencyKey = key };

        // Act - Change state
        notification.Status = NotificationStatus.Processing;
        var keyAfterProcessing = notification.IdempotencyKey;

        notification.Status = NotificationStatus.Sent;
        var keyAfterSent = notification.IdempotencyKey;

        // Assert - Key should remain the same
        Assert.Equal(key, keyAfterProcessing);
        Assert.Equal(key, keyAfterSent);
    }

    [Fact]
    public void IdempotencyKey_CompositeKey_CanBeCreated()
    {
        // Arrange - Simulate composite key pattern (userId + orderId + timestamp)
        var userId = "user-123";
        var orderId = "order-456";
        var timestamp = "20240120100000";
        var compositeKey = $"{userId}:{orderId}:{timestamp}";

        // Act
        var notification = new NotificationMessage { IdempotencyKey = compositeKey };

        // Assert
        Assert.Equal(compositeKey, notification.IdempotencyKey);
        Assert.Contains(userId, notification.IdempotencyKey);
        Assert.Contains(orderId, notification.IdempotencyKey);
    }
}
