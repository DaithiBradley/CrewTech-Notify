using CrewTech.Notify.Core.Interfaces;
using Xunit;

namespace CrewTech.Notify.Core.Tests;

/// <summary>
/// Tests for retry classification and failure categorization
/// </summary>
public class RetryClassificationTests
{
    [Fact]
    public void NotificationResult_Success_HasSuccessTrue()
    {
        // Act
        var result = NotificationResult.Ok();

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void NotificationResult_Fail_HasSuccessFalse()
    {
        // Act
        var result = NotificationResult.Fail("Test error");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Test error", result.ErrorMessage);
    }

    [Theory]
    [InlineData(FailureCategory.NetworkError, true)]
    [InlineData(FailureCategory.ServiceUnavailable, true)]
    [InlineData(FailureCategory.RateLimited, true)]
    [InlineData(FailureCategory.InvalidToken, false)]
    [InlineData(FailureCategory.InvalidPayload, false)]
    [InlineData(FailureCategory.Unauthorized, false)]
    [InlineData(FailureCategory.PlatformNotSupported, false)]
    public void FailureCategory_RetryableClassification_IsCorrect(FailureCategory category, bool expectedRetryable)
    {
        // Arrange - Map expected retryable categories
        var isRetryable = category is FailureCategory.NetworkError 
                                   or FailureCategory.ServiceUnavailable 
                                   or FailureCategory.RateLimited;

        // Assert
        Assert.Equal(expectedRetryable, isRetryable);
    }

    [Fact]
    public void NotificationResult_TransientFailure_IsRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Service temporarily unavailable",
            isRetryable: true,
            errorCode: "503",
            category: FailureCategory.ServiceUnavailable);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.IsRetryable);
        Assert.Equal(FailureCategory.ServiceUnavailable, result.Category);
    }

    [Fact]
    public void NotificationResult_TerminalFailure_NotRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Invalid device token",
            isRetryable: false,
            errorCode: "404",
            category: FailureCategory.InvalidToken);

        // Assert
        Assert.False(result.Success);
        Assert.False(result.IsRetryable);
        Assert.Equal(FailureCategory.InvalidToken, result.Category);
    }

    [Fact]
    public void NotificationResult_NetworkError_IsRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Connection timeout",
            isRetryable: true,
            errorCode: "TIMEOUT",
            category: FailureCategory.NetworkError);

        // Assert
        Assert.True(result.IsRetryable);
        Assert.Equal(FailureCategory.NetworkError, result.Category);
    }

    [Fact]
    public void NotificationResult_RateLimited_IsRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Rate limit exceeded",
            isRetryable: true,
            errorCode: "429",
            category: FailureCategory.RateLimited);

        // Assert
        Assert.True(result.IsRetryable);
        Assert.Equal(FailureCategory.RateLimited, result.Category);
    }

    [Fact]
    public void NotificationResult_InvalidPayload_NotRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Invalid notification format",
            isRetryable: false,
            errorCode: "400",
            category: FailureCategory.InvalidPayload);

        // Assert
        Assert.False(result.IsRetryable);
        Assert.Equal(FailureCategory.InvalidPayload, result.Category);
    }

    [Fact]
    public void NotificationResult_Unauthorized_NotRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Invalid credentials",
            isRetryable: false,
            errorCode: "401",
            category: FailureCategory.Unauthorized);

        // Assert
        Assert.False(result.IsRetryable);
        Assert.Equal(FailureCategory.Unauthorized, result.Category);
    }

    [Fact]
    public void NotificationResult_PlatformNotSupported_NotRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Platform 'Unknown' is not supported",
            isRetryable: false,
            errorCode: "UNSUPPORTED",
            category: FailureCategory.PlatformNotSupported);

        // Assert
        Assert.False(result.IsRetryable);
        Assert.Equal(FailureCategory.PlatformNotSupported, result.Category);
    }

    [Fact]
    public void NotificationResult_UnknownCategory_DefaultsToRetryable()
    {
        // Act
        var result = NotificationResult.Fail(
            "Unknown error",
            isRetryable: true,
            category: FailureCategory.Unknown);

        // Assert
        Assert.True(result.IsRetryable);
        Assert.Equal(FailureCategory.Unknown, result.Category);
    }

    [Fact]
    public void NotificationResult_WithErrorCode_StoresCode()
    {
        // Act
        var result = NotificationResult.Fail(
            "Service error",
            errorCode: "ERR_500");

        // Assert
        Assert.Equal("ERR_500", result.ErrorCode);
    }

    [Fact]
    public void NotificationResult_WithoutErrorCode_HasNullCode()
    {
        // Act
        var result = NotificationResult.Fail("Generic error");

        // Assert
        Assert.Null(result.ErrorCode);
    }

    [Theory]
    [InlineData("503", FailureCategory.ServiceUnavailable, true)]
    [InlineData("500", FailureCategory.ServiceUnavailable, true)]
    [InlineData("429", FailureCategory.RateLimited, true)]
    [InlineData("400", FailureCategory.InvalidPayload, false)]
    [InlineData("401", FailureCategory.Unauthorized, false)]
    [InlineData("404", FailureCategory.InvalidToken, false)]
    public void NotificationResult_HttpStatusCodes_MapToCorrectCategory(
        string statusCode, FailureCategory expectedCategory, bool expectedRetryable)
    {
        // Act
        var result = NotificationResult.Fail(
            $"HTTP {statusCode} error",
            isRetryable: expectedRetryable,
            errorCode: statusCode,
            category: expectedCategory);

        // Assert
        Assert.Equal(expectedCategory, result.Category);
        Assert.Equal(expectedRetryable, result.IsRetryable);
    }

    [Fact]
    public void NotificationResult_MultipleFailureScenarios_PreserveDetails()
    {
        // Arrange - Test different failure scenarios
        var scenarios = new[]
        {
            (Message: "Network timeout", Category: FailureCategory.NetworkError, Retryable: true),
            (Message: "Service down", Category: FailureCategory.ServiceUnavailable, Retryable: true),
            (Message: "Too many requests", Category: FailureCategory.RateLimited, Retryable: true),
            (Message: "Bad token", Category: FailureCategory.InvalidToken, Retryable: false),
            (Message: "Bad request", Category: FailureCategory.InvalidPayload, Retryable: false),
            (Message: "Auth failed", Category: FailureCategory.Unauthorized, Retryable: false)
        };

        foreach (var scenario in scenarios)
        {
            // Act
            var result = NotificationResult.Fail(
                scenario.Message,
                isRetryable: scenario.Retryable,
                category: scenario.Category);

            // Assert
            Assert.Equal(scenario.Message, result.ErrorMessage);
            Assert.Equal(scenario.Category, result.Category);
            Assert.Equal(scenario.Retryable, result.IsRetryable);
        }
    }

    [Fact]
    public void NotificationResult_SuccessResult_HasNoCategory()
    {
        // Act
        var result = NotificationResult.Ok();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(default(FailureCategory), result.Category); // Default is 0 (Unknown)
    }
}
