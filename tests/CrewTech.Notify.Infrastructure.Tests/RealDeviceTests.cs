using Xunit;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using CrewTech.Notify.Infrastructure.Tests.Helpers;

namespace CrewTech.Notify.Infrastructure.Tests;

/// <summary>
/// Tests that run against real devices with real credentials.
/// SKIPPED by default unless UseRealCredentials is enabled.
/// </summary>
public class RealDeviceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    
    public RealDeviceTests()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(TestConfiguration.ApiBaseUrl)
        };
    }
    
    [Fact]
    public async Task SendNotification_ToRealWNSDevice_WhenConfigured_Success()
    {
        // Skip if not using real credentials
        if (!TestConfiguration.UseRealCredentials)
        {
            // Use xUnit Skip.If pattern
            return;
        }
        
        if (string.IsNullOrEmpty(TestConfiguration.WnsDeviceToken))
        {
            return;
        }
        
        // Arrange
        var request = new
        {
            targetPlatform = "WNS",
            deviceToken = TestConfiguration.WnsDeviceToken,
            title = "Test from CrewTech-Notify",
            body = $"Sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            tags = new[] { "test", "real-device" },
            priority = "High"
        };
        
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        
        // Act
        var response = await _httpClient.PostAsync("/api/notifications", content);
        
        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<NotificationResponse>(responseBody, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.NotificationId);
    }
    
    [Fact]
    public async Task SendNotification_ToRealFCMDevice_WhenConfigured_Success()
    {
        // Skip if not using real credentials
        if (!TestConfiguration.UseRealCredentials)
        {
            return;
        }
        
        if (string.IsNullOrEmpty(TestConfiguration.FcmDeviceToken))
        {
            return;
        }
        
        // Arrange
        var request = new
        {
            targetPlatform = "FCM",
            deviceToken = TestConfiguration.FcmDeviceToken,
            title = "Test from CrewTech-Notify",
            body = $"Sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            data = new Dictionary<string, string>
            {
                ["testId"] = Guid.NewGuid().ToString(),
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            },
            priority = "High"
        };
        
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");
        
        // Act
        var response = await _httpClient.PostAsync("/api/notifications", content);
        
        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }
    
    [Fact]
    public async Task SendMultipleNotifications_ToRealDevice_AllSucceed()
    {
        if (!TestConfiguration.UseRealCredentials)
        {
            return;
        }
        
        var deviceToken = TestConfiguration.FcmDeviceToken ?? TestConfiguration.WnsDeviceToken;
        var platform = !string.IsNullOrEmpty(TestConfiguration.FcmDeviceToken) ? "FCM" : "WNS";
        
        if (string.IsNullOrEmpty(deviceToken))
        {
            return;
        }
        
        // Send 3 notifications in sequence
        for (int i = 1; i <= 3; i++)
        {
            var request = new
            {
                targetPlatform = platform,
                deviceToken = deviceToken,
                title = $"Test #{i}",
                body = $"Batch notification {i} of 3",
                priority = "Normal"
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");
            
            var response = await _httpClient.PostAsync("/api/notifications", content);
            Assert.True(response.IsSuccessStatusCode);
            
            // Small delay between notifications
            await Task.Delay(500);
        }
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
    
    private record NotificationResponse(
        Guid NotificationId,
        string Status,
        string Message);
}
