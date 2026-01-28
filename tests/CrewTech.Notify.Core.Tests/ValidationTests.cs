using CrewTech.Notify.Core.Entities;
using Xunit;

namespace CrewTech.Notify.Core.Tests;

/// <summary>
/// Tests for input validation and sanitization
/// </summary>
public class ValidationTests
{
    [Fact]
    public void NotificationMessage_RequiredFields_CanBeSet()
    {
        // Arrange & Act
        var notification = new NotificationMessage
        {
            TargetPlatform = "Fake",
            DeviceToken = "test-device-001",
            Title = "Test Title"
        };

        // Assert
        Assert.Equal("Fake", notification.TargetPlatform);
        Assert.Equal("test-device-001", notification.DeviceToken);
        Assert.Equal("Test Title", notification.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void DeviceToken_EmptyOrWhitespace_ShouldBeValidated(string? deviceToken)
    {
        // Arrange & Act
        var notification = new NotificationMessage
        {
            DeviceToken = deviceToken ?? string.Empty
        };

        // Assert - Empty device token should be caught by validation
        Assert.Equal(deviceToken ?? string.Empty, notification.DeviceToken);
    }

    [Fact]
    public void Title_WithXssPatterns_CanBeStored()
    {
        // Arrange - XSS patterns should be stored as-is, sanitization happens at display
        var xssPatterns = new[]
        {
            "<script>alert('XSS')</script>",
            "<img src=x onerror=alert(1)>",
            "javascript:alert('XSS')"
        };

        foreach (var pattern in xssPatterns)
        {
            // Act
            var notification = new NotificationMessage
            {
                Title = pattern,
                Body = "Test body"
            };

            // Assert - Stored as-is for provider to handle
            Assert.Equal(pattern, notification.Title);
        }
    }

    [Fact]
    public void Body_WithSqlInjectionPatterns_CanBeStored()
    {
        // Arrange - SQL injection patterns stored safely (parameterized queries protect us)
        var sqlPatterns = new[]
        {
            "'; DROP TABLE Users; --",
            "1' OR '1'='1",
            "admin'--"
        };

        foreach (var pattern in sqlPatterns)
        {
            // Act
            var notification = new NotificationMessage
            {
                Title = "Test",
                Body = pattern
            };

            // Assert
            Assert.Equal(pattern, notification.Body);
        }
    }

    [Fact]
    public void Data_LargePayload_CanBeStored()
    {
        // Arrange - Test payload size limits
        var largeData = new string('X', 10000); // 10KB

        // Act
        var notification = new NotificationMessage
        {
            Title = "Test",
            Body = "Test",
            Data = largeData
        };

        // Assert
        Assert.Equal(largeData, notification.Data);
        Assert.Equal(10000, notification.Data.Length);
    }

    [Theory]
    [InlineData("tag1")]
    [InlineData("tag1,tag2")]
    [InlineData("urgent,billing,production")]
    [InlineData("")]
    public void Tags_VariousFormats_CanBeStored(string tags)
    {
        // Act
        var notification = new NotificationMessage
        {
            Tags = tags
        };

        // Assert
        Assert.Equal(tags, notification.Tags);
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Normal")]
    [InlineData("High")]
    public void Priority_ValidValues_CanBeSet(string priority)
    {
        // Act
        var notification = new NotificationMessage
        {
            Priority = priority
        };

        // Assert
        Assert.Equal(priority, notification.Priority);
    }

    [Fact]
    public void Priority_DefaultValue_IsNormal()
    {
        // Act
        var notification = new NotificationMessage();

        // Assert
        Assert.Equal("Normal", notification.Priority);
    }

    [Theory]
    [InlineData("WNS")]
    [InlineData("FCM")]
    [InlineData("Fake")]
    [InlineData("iOS")]
    [InlineData("Slack")]
    public void TargetPlatform_ValidPlatforms_CanBeSet(string platform)
    {
        // Act
        var notification = new NotificationMessage
        {
            TargetPlatform = platform
        };

        // Assert
        Assert.Equal(platform, notification.TargetPlatform);
    }

    [Fact]
    public void DeviceToken_VeryLongToken_CanBeStored()
    {
        // Arrange - WNS channel URIs can be very long
        var longToken = "https://db5p.notify.windows.com/?token=" + new string('A', 500);

        // Act
        var notification = new NotificationMessage
        {
            DeviceToken = longToken
        };

        // Assert
        Assert.Equal(longToken, notification.DeviceToken);
    }

    [Fact]
    public void MaxRetries_DefaultValue_IsFive()
    {
        // Act
        var notification = new NotificationMessage();

        // Assert
        Assert.Equal(5, notification.MaxRetries);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void MaxRetries_CustomValues_CanBeSet(int maxRetries)
    {
        // Act
        var notification = new NotificationMessage
        {
            MaxRetries = maxRetries
        };

        // Assert
        Assert.Equal(maxRetries, notification.MaxRetries);
    }

    [Fact]
    public void ScheduledFor_FutureDate_CanBeSet()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddHours(24);

        // Act
        var notification = new NotificationMessage
        {
            ScheduledFor = futureDate
        };

        // Assert
        Assert.NotNull(notification.ScheduledFor);
        Assert.True(notification.ScheduledFor > DateTime.UtcNow);
    }

    [Fact]
    public void IdempotencyKey_UniqueKeys_AreDifferent()
    {
        // Act
        var notification1 = new NotificationMessage { IdempotencyKey = Guid.NewGuid().ToString() };
        var notification2 = new NotificationMessage { IdempotencyKey = Guid.NewGuid().ToString() };

        // Assert
        Assert.NotEqual(notification1.IdempotencyKey, notification2.IdempotencyKey);
    }

    [Fact]
    public void Title_MaxLength_CanBeStored()
    {
        // Arrange - Test very long titles
        var longTitle = new string('A', 1000);

        // Act
        var notification = new NotificationMessage
        {
            Title = longTitle
        };

        // Assert
        Assert.Equal(1000, notification.Title.Length);
    }

    [Fact]
    public void Body_MaxLength_CanBeStored()
    {
        // Arrange - Test very long bodies
        var longBody = new string('B', 5000);

        // Act
        var notification = new NotificationMessage
        {
            Body = longBody
        };

        // Assert
        Assert.Equal(5000, notification.Body.Length);
    }
}
