using CrewTech.Notify.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CrewTech.Notify.Infrastructure.Providers;

/// <summary>
/// Windows Push Notification Service (WNS) provider
/// </summary>
public class WnsNotificationProvider : INotificationProvider
{
    private readonly ILogger<WnsNotificationProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly WnsConfiguration _configuration;
    private string? _accessToken;
    private DateTime _tokenExpiresAt;
    
    public WnsNotificationProvider(
        ILogger<WnsNotificationProvider> logger,
        HttpClient httpClient,
        WnsConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }
    
    public string Platform => "WNS";
    
    public async Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure we have a valid access token
            await EnsureAccessTokenAsync(cancellationToken);
            
            // Create WNS toast notification XML
            var toastXml = CreateToastXml(title, body, data);
            
            // Send to WNS
            var request = new HttpRequestMessage(HttpMethod.Post, deviceToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("X-WNS-Type", "wns/toast");
            request.Content = new StringContent(toastXml, Encoding.UTF8, "text/xml");
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WNS notification sent successfully to {DeviceToken}", deviceToken);
                return NotificationResult.Ok();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Map HTTP status codes to failure categories
            var category = response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => FailureCategory.InvalidPayload,
                System.Net.HttpStatusCode.Unauthorized => FailureCategory.Unauthorized,
                System.Net.HttpStatusCode.NotFound => FailureCategory.InvalidToken,
                System.Net.HttpStatusCode.TooManyRequests => FailureCategory.RateLimited,
                System.Net.HttpStatusCode.ServiceUnavailable => FailureCategory.ServiceUnavailable,
                System.Net.HttpStatusCode.InternalServerError => FailureCategory.ServiceUnavailable,
                _ => FailureCategory.Unknown
            };
            
            var isRetryable = category is FailureCategory.ServiceUnavailable 
                                       or FailureCategory.RateLimited 
                                       or FailureCategory.Unknown;
            
            _logger.LogWarning("WNS notification failed: {StatusCode} ({Category}) - {Error}", 
                response.StatusCode, category, errorContent);
            
            return NotificationResult.Fail(
                $"WNS error: {response.StatusCode} - {errorContent}",
                isRetryable: isRetryable,
                errorCode: response.StatusCode.ToString(),
                category: category);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error sending WNS notification to {DeviceToken}", deviceToken);
            return NotificationResult.Fail(ex.Message, isRetryable: true, category: FailureCategory.NetworkError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending WNS notification to {DeviceToken}", deviceToken);
            return NotificationResult.Fail(ex.Message, isRetryable: true, category: FailureCategory.Unknown);
        }
    }
    
    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresAt)
        {
            return; // Token is still valid
        }
        
        // Get new access token from Azure AD
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _configuration.ClientId),
            new KeyValuePair<string, string>("client_secret", _configuration.ClientSecret),
            new KeyValuePair<string, string>("scope", "https://wns.windows.com/.default")
        });
        
        var response = await _httpClient.PostAsync(
            $"https://login.microsoftonline.com/{_configuration.TenantId}/oauth2/v2.0/token",
            tokenRequest,
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        var tokenResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenResponse);
        
        _accessToken = tokenData.GetProperty("access_token").GetString();
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300); // Refresh 5 minutes early
    }
    
    private string CreateToastXml(string title, string body, Dictionary<string, string>? data)
    {
        var xml = new StringBuilder();
        xml.Append("<toast>");
        xml.Append("<visual>");
        xml.Append("<binding template=\"ToastGeneric\">");
        xml.AppendFormat("<text>{0}</text>", System.Security.SecurityElement.Escape(title));
        xml.AppendFormat("<text>{0}</text>", System.Security.SecurityElement.Escape(body));
        xml.Append("</binding>");
        xml.Append("</visual>");
        
        if (data != null && data.Count > 0)
        {
            xml.Append("<actions>");
            foreach (var kvp in data)
            {
                xml.AppendFormat("<action content=\"{0}\" arguments=\"{1}\" />",
                    System.Security.SecurityElement.Escape(kvp.Key),
                    System.Security.SecurityElement.Escape(kvp.Value));
            }
            xml.Append("</actions>");
        }
        
        xml.Append("</toast>");
        return xml.ToString();
    }
}

/// <summary>
/// Configuration for WNS provider
/// </summary>
public class WnsConfiguration
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
