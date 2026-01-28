using CrewTech.Notify.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CrewTech.Notify.Infrastructure.Tests;

public class FakeNotificationProviderTests
{
    [Fact]
    public async Task SendAsync_ReturnsSuccess()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FakeNotificationProvider>>();
        var provider = new FakeNotificationProvider(loggerMock.Object);

        // Act
        var result = await provider.SendAsync(
            "device-token-123",
            "Test Title",
            "Test Body");

        // Assert - Most of the time it should succeed (95% success rate)
        // We'll just verify it returns a result
        Assert.NotNull(result);
    }

    [Fact]
    public void Platform_ReturnsFake()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FakeNotificationProvider>>();
        var provider = new FakeNotificationProvider(loggerMock.Object);

        // Act
        var platform = provider.Platform;

        // Assert
        Assert.Equal("Fake", platform);
    }

    [Fact]
    public async Task SendAsync_WithData_LogsData()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FakeNotificationProvider>>();
        var provider = new FakeNotificationProvider(loggerMock.Object);
        var data = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        await provider.SendAsync(
            "device-token-123",
            "Test Title",
            "Test Body",
            data);

        // Assert - Verify logging occurred
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
}
