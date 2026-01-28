using CrewTech.Notify.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CrewTech.Notify.Infrastructure.Providers;

/// <summary>
/// Firebase Cloud Messaging (FCM) provider for Android notifications
/// </summary>
public class FcmNotificationProvider : INotificationProvider
{
    private readonly ILogger<FcmNotificationProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly FcmConfiguration _configuration;
    
    public FcmNotificationProvider(
        ILogger<FcmNotificationProvider> logger,
        HttpClient httpClient,
        FcmConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }
    
    public string Platform => "FCM";
    
    public async Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new
                    {
                        title,
                        body
                    },
                    data = data ?? new Dictionary<string, string>(),
                    android = new
                    {
                        priority = "high"
                    }
                }
            };
            
            var json = JsonSerializer.Serialize(message);
            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"https://fcm.googleapis.com/v1/projects/{_configuration.ProjectId}/messages:send");
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ServerKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("FCM notification sent successfully to {DeviceToken}", deviceToken);
                return NotificationResult.Ok();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var isRetryable = response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                             response.StatusCode == System.Net.HttpStatusCode.InternalServerError ||
                             response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
            
            _logger.LogWarning("FCM notification failed: {StatusCode} - {Error}", 
                response.StatusCode, errorContent);
            
            return NotificationResult.Fail(
                $"FCM error: {response.StatusCode} - {errorContent}",
                isRetryable: isRetryable,
                errorCode: response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending FCM notification to {DeviceToken}", deviceToken);
            return NotificationResult.Fail(ex.Message, isRetryable: true);
        }
    }
}

/// <summary>
/// Configuration for FCM provider
/// </summary>
public class FcmConfiguration
{
    public string ProjectId { get; set; } = string.Empty;
    public string ServerKey { get; set; } = string.Empty;
}
