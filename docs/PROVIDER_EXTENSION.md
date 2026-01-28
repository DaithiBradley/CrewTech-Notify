# Provider Extension Guide

This guide shows how to extend CrewTech-Notify with new notification providers for platforms like Slack, Zulip, Mattermost, email, SMS, or custom messaging systems.

## Overview

The provider architecture is designed for extensibility:
- **Platform-agnostic interface**: `INotificationProvider` works for any platform
- **Error classification**: Map platform errors to standard categories
- **Automatic retry handling**: Worker handles retries based on error classification
- **Flexible device tokens**: Platform-specific addressing (channels, emails, phone numbers)

## Quick Start: Adding a Slack Provider

### Step 1: Implement INotificationProvider

Create `src/CrewTech.Notify.Infrastructure/Providers/SlackNotificationProvider.cs`:

```csharp
using CrewTech.Notify.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CrewTech.Notify.Infrastructure.Providers;

/// <summary>
/// Slack webhook notification provider
/// </summary>
public class SlackNotificationProvider : INotificationProvider
{
    private readonly ILogger<SlackNotificationProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public SlackNotificationProvider(
        ILogger<SlackNotificationProvider> logger,
        HttpClient httpClient,
        string webhookUrl)
    {
        _logger = logger;
        _httpClient = httpClient;
        _webhookUrl = webhookUrl;
    }

    public string Platform => "Slack";

    public async Task<NotificationResult> SendAsync(
        string deviceToken, // Slack channel or user ID
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build Slack message with markdown formatting
            var message = new
            {
                channel = deviceToken, // e.g., "#general" or "@username"
                text = $"*{title}*",
                blocks = new[]
                {
                    new
                    {
                        type = "header",
                        text = new
                        {
                            type = "plain_text",
                            text = title
                        }
                    },
                    new
                    {
                        type = "section",
                        text = new
                        {
                            type = "mrkdwn",
                            text = body
                        }
                    }
                }
            };

            // Add custom data fields if provided
            if (data != null && data.Count > 0)
            {
                var fields = data.Select(kv => new
                {
                    type = "mrkdwn",
                    text = $"*{kv.Key}:* {kv.Value}"
                }).ToArray();

                // Note: In real implementation, add fields to blocks array
            }

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Slack notification sent to {Channel}: {Title}", deviceToken, title);
                return NotificationResult.Ok();
            }

            // Map HTTP status to failure category
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var category = response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => FailureCategory.RateLimited,
                HttpStatusCode.BadRequest => FailureCategory.InvalidPayload,
                HttpStatusCode.NotFound => FailureCategory.InvalidToken,
                HttpStatusCode.Unauthorized => FailureCategory.Unauthorized,
                HttpStatusCode.ServiceUnavailable => FailureCategory.ServiceUnavailable,
                HttpStatusCode.InternalServerError => FailureCategory.ServiceUnavailable,
                _ => FailureCategory.Unknown
            };

            var isRetryable = category is FailureCategory.ServiceUnavailable
                                       or FailureCategory.RateLimited
                                       or FailureCategory.Unknown;

            _logger.LogWarning("Slack notification failed: {StatusCode} ({Category}) - {Error}",
                response.StatusCode, category, errorContent);

            return NotificationResult.Fail(
                $"Slack error: {response.StatusCode} - {errorContent}",
                isRetryable: isRetryable,
                errorCode: response.StatusCode.ToString(),
                category: category);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error sending Slack notification to {Channel}", deviceToken);
            return NotificationResult.Fail(ex.Message, isRetryable: true, category: FailureCategory.NetworkError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending Slack notification to {Channel}", deviceToken);
            return NotificationResult.Fail(ex.Message, isRetryable: true, category: FailureCategory.Unknown);
        }
    }
}

/// <summary>
/// Configuration for Slack provider
/// </summary>
public class SlackConfiguration
{
    public string WebhookUrl { get; set; } = string.Empty;
}
```

### Step 2: Register Provider in Dependency Injection

**API**: `src/CrewTech.Notify.SenderApi/Program.cs`

```csharp
// Configure Slack provider
var slackConfig = builder.Configuration.GetSection("Slack").Get<SlackConfiguration>();
if (slackConfig != null && !string.IsNullOrEmpty(slackConfig.WebhookUrl))
{
    builder.Services.AddSingleton<INotificationProvider>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<SlackNotificationProvider>>();
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        return new SlackNotificationProvider(logger, httpClient, slackConfig.WebhookUrl);
    });
}
```

**Worker**: `src/CrewTech.Notify.Worker/Program.cs`

```csharp
// Configure Slack provider (same as API)
var slackConfig = builder.Configuration.GetSection("Slack").Get<SlackConfiguration>();
if (slackConfig != null && !string.IsNullOrEmpty(slackConfig.WebhookUrl))
{
    builder.Services.AddSingleton<INotificationProvider>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<SlackNotificationProvider>>();
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
        return new SlackNotificationProvider(logger, httpClient, slackConfig.WebhookUrl);
    });
}
```

### Step 3: Add Configuration

**`appsettings.json`** (API and Worker):

```json
{
  "Slack": {
    "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
  }
}
```

**For production** (`appsettings.Production.json`):
```json
{
  "Slack": {
    "WebhookUrl": "${SLACK_WEBHOOK_URL}" // Use environment variable
  }
}
```

### Step 4: Test the Provider

```bash
# Send a test notification
curl -X POST http://localhost:5000/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "targetPlatform": "Slack",
    "deviceToken": "#general",
    "title": "Deployment Complete",
    "body": "Version 2.0 is now live in production! :rocket:",
    "data": {
      "version": "2.0.0",
      "environment": "production"
    }
  }'
```

Expected response:
```json
{
  "notificationId": "guid-here",
  "status": "Accepted",
  "message": "Notification queued for delivery"
}
```

## Advanced Examples

### Zulip Provider

```csharp
public class ZulipNotificationProvider : INotificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl; // e.g., https://yourdomain.zulipchat.com/api/v1
    private readonly string _botEmail;
    private readonly string _botApiKey;

    public string Platform => "Zulip";

    public async Task<NotificationResult> SendAsync(
        string deviceToken, // Stream name: "general" or DM: "user@example.com"
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        // Build Zulip message (supports Markdown)
        var message = deviceToken.Contains("@")
            ? new { type = "private", to = deviceToken, content = $"**{title}**\n\n{body}" }
            : new { type = "stream", to = deviceToken, subject = title, content = body };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_botEmail}:{_botApiKey}"))
        );

        // Implementation similar to Slack...
        // Map status codes to FailureCategory
        // Return NotificationResult
    }
}
```

### Email Provider (SMTP)

```csharp
public class EmailNotificationProvider : INotificationProvider
{
    private readonly SmtpClient _smtpClient;
    private readonly string _fromAddress;

    public string Platform => "Email";

    public async Task<NotificationResult> SendAsync(
        string deviceToken, // Email address
        string title,       // Email subject
        string body,        // Email body (HTML or plain text)
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MailMessage(_fromAddress, deviceToken, title, body)
            {
                IsBodyHtml = data?.ContainsKey("isHtml") == true
            };

            await _smtpClient.SendMailAsync(message, cancellationToken);
            return NotificationResult.Ok();
        }
        catch (SmtpException ex)
        {
            // Classify SMTP errors
            var category = ex.StatusCode switch
            {
                SmtpStatusCode.MailboxUnavailable => FailureCategory.InvalidToken,
                SmtpStatusCode.ServiceNotAvailable => FailureCategory.ServiceUnavailable,
                _ => FailureCategory.Unknown
            };

            return NotificationResult.Fail(ex.Message, category: category);
        }
    }
}
```

### SMS Provider (Twilio)

```csharp
public class TwilioSmsProvider : INotificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;

    public string Platform => "SMS";

    public async Task<NotificationResult> SendAsync(
        string deviceToken, // Phone number: "+1234567890"
        string title,       // Not used for SMS
        string body,        // SMS text (160 chars recommended)
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json");

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_accountSid}:{_authToken}"))
        );

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "From", _fromNumber },
            { "To", deviceToken },
            { "Body", body }
        });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        // Implementation similar to other providers...
    }
}
```

## Platform-Agnostic Design

### Device Token Flexibility

The `deviceToken` parameter is platform-specific:

| Platform    | deviceToken Format                    | Example                                      |
|-------------|---------------------------------------|----------------------------------------------|
| **WNS**     | Channel URI (URL)                     | `https://db5p.notify.windows.com/?token=...` |
| **FCM**     | Firebase registration token (string)  | `dGVzdC10b2tlbi1hYmNkZWY...`                 |
| **Slack**   | Channel (#) or user (@)               | `#general`, `@john.doe`                      |
| **Zulip**   | Stream name or email                  | `general`, `user@example.com`                |
| **Email**   | Email address                         | `user@example.com`                           |
| **SMS**     | Phone number (E.164 format)           | `+12345678901`                               |
| **Fake**    | Any string                            | `test-device-001`                            |

### Data Payload Usage

The `data` dictionary can hold platform-specific metadata:

**Slack Example:**
```json
{
  "data": {
    "color": "#36a64f",
    "emoji": ":rocket:",
    "mention": "@here"
  }
}
```

**Email Example:**
```json
{
  "data": {
    "isHtml": "true",
    "cc": "team@example.com",
    "priority": "high"
  }
}
```

## Error Classification Best Practices

### Mapping Platform Errors

Always map platform-specific errors to standard `FailureCategory`:

```csharp
var category = platformErrorCode switch
{
    // Retryable errors
    "RATE_LIMIT" => FailureCategory.RateLimited,
    "TIMEOUT" => FailureCategory.NetworkError,
    "SERVICE_DOWN" => FailureCategory.ServiceUnavailable,

    // Terminal errors (don't retry)
    "INVALID_TOKEN" => FailureCategory.InvalidToken,
    "BAD_REQUEST" => FailureCategory.InvalidPayload,
    "AUTH_FAILED" => FailureCategory.Unauthorized,

    // Unknown = retry by default
    _ => FailureCategory.Unknown
};
```

### Setting IsRetryable Flag

```csharp
var isRetryable = category is FailureCategory.ServiceUnavailable
                           or FailureCategory.RateLimited
                           or FailureCategory.NetworkError
                           or FailureCategory.Unknown;
```

## Testing Your Provider

### Unit Tests

Create `tests/CrewTech.Notify.Infrastructure.Tests/SlackNotificationProviderTests.cs`:

```csharp
public class SlackNotificationProviderTests
{
    [Fact]
    public async Task SendAsync_SuccessfulResponse_ReturnsOk()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("application/json", "{ \"ok\": true }");

        var httpClient = mockHttp.ToHttpClient();
        var logger = Mock.Of<ILogger<SlackNotificationProvider>>();
        var provider = new SlackNotificationProvider(logger, httpClient, "https://test.slack.com/webhook");

        // Act
        var result = await provider.SendAsync("#test", "Title", "Body");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task SendAsync_RateLimited_ReturnsRetryableFailure()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(HttpStatusCode.TooManyRequests);

        var httpClient = mockHttp.ToHttpClient();
        var logger = Mock.Of<ILogger<SlackNotificationProvider>>();
        var provider = new SlackNotificationProvider(logger, httpClient, "https://test.slack.com/webhook");

        // Act
        var result = await provider.SendAsync("#test", "Title", "Body");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.IsRetryable);
        Assert.Equal(FailureCategory.RateLimited, result.Category);
    }
}
```

### Integration Testing

Test with real Slack workspace (use test channel):

```bash
# Set webhook URL
export SLACK_WEBHOOK_URL="https://hooks.slack.com/services/YOUR/TEST/WEBHOOK"

# Start API and Worker
dotnet run --project src/CrewTech.Notify.SenderApi &
dotnet run --project src/CrewTech.Notify.Worker &

# Send test notification
curl -X POST http://localhost:5000/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "targetPlatform": "Slack",
    "deviceToken": "#test-notifications",
    "title": "Integration Test",
    "body": "If you see this, the Slack provider works!"
  }'

# Check Slack for message
```

## Configuration Patterns

### Environment-Specific Configuration

```json
// appsettings.Development.json
{
  "Slack": {
    "WebhookUrl": "https://hooks.slack.com/services/TEST/DEV/WEBHOOK"
  }
}

// appsettings.Production.json
{
  "Slack": {
    "WebhookUrl": "${SLACK_WEBHOOK_URL}" // From environment variable
  }
}
```

### Multiple Providers for Same Platform

```csharp
// Register multiple Slack providers for different workspaces
builder.Services.AddSingleton<INotificationProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SlackNotificationProvider>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var webhook = builder.Configuration["Slack:Engineering:WebhookUrl"];
    return new SlackNotificationProvider(logger, httpClient, webhook);
});

builder.Services.AddSingleton<INotificationProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SlackNotificationProvider>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var webhook = builder.Configuration["Slack:Sales:WebhookUrl"];
    return new SlackNotificationProvider(logger, httpClient, webhook);
});
```

## Common Pitfalls

### ❌ Don't: Return generic errors
```csharp
catch (Exception ex)
{
    return NotificationResult.Fail(ex.Message); // Missing category!
}
```

### ✅ Do: Always classify errors
```csharp
catch (HttpRequestException ex)
{
    return NotificationResult.Fail(ex.Message, isRetryable: true, category: FailureCategory.NetworkError);
}
catch (Exception ex)
{
    return NotificationResult.Fail(ex.Message, isRetryable: true, category: FailureCategory.Unknown);
}
```

### ❌ Don't: Log sensitive data
```csharp
_logger.LogError("Failed to send to {DeviceToken}: {ApiKey}", deviceToken, apiKey); // API key exposed!
```

### ✅ Do: Log safely
```csharp
_logger.LogError("Failed to send to {DeviceToken}", deviceToken.Substring(0, Math.Min(10, deviceToken.Length)) + "...");
```

## See Also
- [Retry Strategy Documentation](RETRY_STRATEGY.md)
- [Architecture Overview](../README.md#architecture)
- [INotificationProvider Interface](../src/CrewTech.Notify.Core/Interfaces/INotificationProvider.cs)
