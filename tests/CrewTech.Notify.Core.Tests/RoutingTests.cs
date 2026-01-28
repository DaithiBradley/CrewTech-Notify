using CrewTech.Notify.Core.Interfaces;
using CrewTech.Notify.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CrewTech.Notify.Core.Tests;

/// <summary>
/// Tests for notification provider routing and factory
/// </summary>
public class RoutingTests
{
    [Fact]
    public void NotificationProviderFactory_GetProvider_ReturnsCorrectProvider()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FakeNotificationProvider>>();
        var fakeProvider = new FakeNotificationProvider(mockLogger.Object);
        var providers = new List<INotificationProvider> { fakeProvider };
        var factory = new NotificationProviderFactory(providers);

        // Act
        var provider = factory.GetProvider("Fake");

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("Fake", provider.Platform);
    }

    [Fact]
    public void NotificationProviderFactory_GetProvider_CaseInsensitive()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FakeNotificationProvider>>();
        var fakeProvider = new FakeNotificationProvider(mockLogger.Object);
        var providers = new List<INotificationProvider> { fakeProvider };
        var factory = new NotificationProviderFactory(providers);

        // Act & Assert - All case variations should work
        Assert.NotNull(factory.GetProvider("fake"));
        Assert.NotNull(factory.GetProvider("FAKE"));
        Assert.NotNull(factory.GetProvider("Fake"));
        Assert.NotNull(factory.GetProvider("FaKe"));
    }

    [Fact]
    public void NotificationProviderFactory_GetProvider_UnknownPlatform_ReturnsNull()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FakeNotificationProvider>>();
        var fakeProvider = new FakeNotificationProvider(mockLogger.Object);
        var providers = new List<INotificationProvider> { fakeProvider };
        var factory = new NotificationProviderFactory(providers);

        // Act
        var provider = factory.GetProvider("UnknownPlatform");

        // Assert
        Assert.Null(provider);
    }

    [Fact]
    public void NotificationProviderFactory_GetSupportedPlatforms_ReturnsAllPlatforms()
    {
        // Arrange
        var mockProviders = new List<INotificationProvider>
        {
            CreateMockProvider("WNS"),
            CreateMockProvider("FCM"),
            CreateMockProvider("Fake")
        };
        var factory = new NotificationProviderFactory(mockProviders);

        // Act
        var platforms = factory.GetSupportedPlatforms().ToList();

        // Assert
        Assert.Equal(3, platforms.Count);
        Assert.Contains("WNS", platforms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("FCM", platforms, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Fake", platforms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotificationProviderFactory_GetAllProviders_ReturnsAllRegistered()
    {
        // Arrange
        var mockProviders = new List<INotificationProvider>
        {
            CreateMockProvider("WNS"),
            CreateMockProvider("FCM"),
            CreateMockProvider("Fake")
        };
        var factory = new NotificationProviderFactory(mockProviders);

        // Act
        var providers = factory.GetAllProviders().ToList();

        // Assert
        Assert.Equal(3, providers.Count);
    }

    [Fact]
    public void NotificationProviderFactory_EmptyProviders_GetProviderReturnsNull()
    {
        // Arrange
        var factory = new NotificationProviderFactory(new List<INotificationProvider>());

        // Act
        var provider = factory.GetProvider("Fake");

        // Assert
        Assert.Null(provider);
    }

    [Fact]
    public void NotificationProviderFactory_EmptyProviders_GetSupportedPlatformsReturnsEmpty()
    {
        // Arrange
        var factory = new NotificationProviderFactory(new List<INotificationProvider>());

        // Act
        var platforms = factory.GetSupportedPlatforms().ToList();

        // Assert
        Assert.Empty(platforms);
    }

    [Fact]
    public void NotificationProviderFactory_MultipleProviders_RoutesToCorrectOne()
    {
        // Arrange
        var providers = new List<INotificationProvider>
        {
            CreateMockProvider("WNS"),
            CreateMockProvider("FCM"),
            CreateMockProvider("Fake"),
            CreateMockProvider("iOS")
        };
        var factory = new NotificationProviderFactory(providers);

        // Act & Assert
        Assert.Equal("WNS", factory.GetProvider("WNS")?.Platform);
        Assert.Equal("FCM", factory.GetProvider("FCM")?.Platform);
        Assert.Equal("Fake", factory.GetProvider("Fake")?.Platform);
        Assert.Equal("iOS", factory.GetProvider("iOS")?.Platform);
    }

    [Theory]
    [InlineData("WNS")]
    [InlineData("FCM")]
    [InlineData("Fake")]
    [InlineData("iOS")]
    [InlineData("Slack")]
    public void NotificationProviderFactory_SingleProvider_RoutesCorrectly(string platform)
    {
        // Arrange
        var provider = CreateMockProvider(platform);
        var factory = new NotificationProviderFactory(new[] { provider });

        // Act
        var result = factory.GetProvider(platform);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(platform, result.Platform);
    }

    [Fact]
    public void NotificationProviderFactory_DuplicatePlatform_UsesLastRegistered()
    {
        // Arrange - Simulate duplicate platform registration (should not happen but test behavior)
        var provider1 = CreateMockProvider("Fake", "Provider1");
        var provider2 = CreateMockProvider("Fake", "Provider2");
        
        // Act - The factory uses ToDictionary which will throw on duplicates
        // We expect an exception here
        Assert.Throws<ArgumentException>(() => 
            new NotificationProviderFactory(new[] { provider1, provider2 }));
    }

    [Fact]
    public void FakeProvider_Platform_ReturnsFake()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FakeNotificationProvider>>();
        var provider = new FakeNotificationProvider(mockLogger.Object);

        // Assert
        Assert.Equal("Fake", provider.Platform);
    }

    [Fact]
    public void NotificationProviderFactory_GetProvider_WithNull_ThrowsException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FakeNotificationProvider>>();
        var fakeProvider = new FakeNotificationProvider(mockLogger.Object);
        var factory = new NotificationProviderFactory(new[] { fakeProvider });

        // Act & Assert - Dictionary throws ArgumentNullException on null key
        Assert.Throws<ArgumentNullException>(() => factory.GetProvider(null!));
    }

    [Fact]
    public void NotificationProviderFactory_GetProvider_WithEmptyString_ReturnsNull()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FakeNotificationProvider>>();
        var fakeProvider = new FakeNotificationProvider(mockLogger.Object);
        var factory = new NotificationProviderFactory(new[] { fakeProvider });

        // Act
        var provider = factory.GetProvider(string.Empty);

        // Assert
        Assert.Null(provider);
    }

    private INotificationProvider CreateMockProvider(string platform, string identifier = "")
    {
        var mock = new Mock<INotificationProvider>();
        mock.Setup(p => p.Platform).Returns(platform);
        mock.Setup(p => p.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationResult.Ok());
        return mock.Object;
    }
}
