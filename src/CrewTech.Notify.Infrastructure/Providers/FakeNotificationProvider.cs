using CrewTech.Notify.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CrewTech.Notify.Infrastructure.Providers;

/// <summary>
/// Fake notification provider for local development and testing
/// </summary>
public class FakeNotificationProvider : INotificationProvider
{
    private readonly ILogger<FakeNotificationProvider> _logger;
    
    public FakeNotificationProvider(ILogger<FakeNotificationProvider> logger)
    {
        _logger = logger;
    }
    
    public string Platform => "Fake";
    
    public Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "📱 Fake notification sent to {DeviceToken}: {Title} - {Body}",
            deviceToken, title, body);
        
        if (data != null && data.Count > 0)
        {
            _logger.LogInformation("   Data: {Data}", string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        
        // Simulate occasional failures for testing retry logic
        // Use Random.Shared for thread-safety
        if (Random.Shared.Next(100) < 5) // 5% failure rate
        {
            _logger.LogWarning("   ⚠️  Simulated transient failure");
            return Task.FromResult(NotificationResult.Fail(
                "Simulated transient failure",
                isRetryable: true,
                errorCode: "FAKE_TRANSIENT",
                category: FailureCategory.ServiceUnavailable));
        }
        
        return Task.FromResult(NotificationResult.Ok());
    }
}
