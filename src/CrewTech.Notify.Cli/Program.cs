using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;

var rootCommand = new RootCommand("CrewTech Notify CLI - Send notifications from the command line");

// Send command
var sendCommand = new Command("send", "Send a notification");

var platformOption = new Option<string>(
    aliases: new[] { "--platform", "-p" },
    description: "Target platform (WNS, FCM, Fake)",
    getDefaultValue: () => "Fake");

var deviceTokenOption = new Option<string>(
    aliases: new[] { "--device-token", "-d" },
    description: "Device token or channel URI")
{ IsRequired = true };

var titleOption = new Option<string>(
    aliases: new[] { "--title", "-t" },
    description: "Notification title")
{ IsRequired = true };

var bodyOption = new Option<string>(
    aliases: new[] { "--body", "-b" },
    description: "Notification body")
{ IsRequired = true };

var tagsOption = new Option<string[]>(
    aliases: new[] { "--tags" },
    description: "Tags (comma-separated)")
{ AllowMultipleArgumentsPerToken = true };

var priorityOption = new Option<string>(
    aliases: new[] { "--priority" },
    description: "Priority level (Low, Normal, High)",
    getDefaultValue: () => "Normal");

var apiUrlOption = new Option<string>(
    aliases: new[] { "--api-url", "-u" },
    description: "API base URL",
    getDefaultValue: () => "http://localhost:5000");

sendCommand.AddOption(platformOption);
sendCommand.AddOption(deviceTokenOption);
sendCommand.AddOption(titleOption);
sendCommand.AddOption(bodyOption);
sendCommand.AddOption(tagsOption);
sendCommand.AddOption(priorityOption);
sendCommand.AddOption(apiUrlOption);

sendCommand.SetHandler(async (platform, deviceToken, title, body, tags, priority, apiUrl) =>
{
    try
    {
        Console.WriteLine("📤 Sending notification...");
        Console.WriteLine($"   Platform: {platform}");
        Console.WriteLine($"   Title: {title}");
        Console.WriteLine($"   Body: {body}");

        var request = new
        {
            targetPlatform = platform,
            deviceToken = deviceToken,
            title = title,
            body = body,
            tags = tags?.ToList(),
            priority = priority
        };

        using var httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var response = await httpClient.PostAsJsonAsync("/api/notifications", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var notificationId = result.GetProperty("notificationId").GetGuid();
            Console.WriteLine($"✓ Notification queued successfully!");
            Console.WriteLine($"   Notification ID: {notificationId}");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"✗ Failed to send notification: {response.StatusCode}");
            Console.WriteLine($"   Error: {error}");
            Environment.Exit(1);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error: {ex.Message}");
        Environment.Exit(1);
    }
}, platformOption, deviceTokenOption, titleOption, bodyOption, tagsOption, priorityOption, apiUrlOption);

// Status command
var statusCommand = new Command("status", "Check notification status");

var notificationIdOption = new Option<Guid>(
    aliases: new[] { "--id", "-i" },
    description: "Notification ID")
{ IsRequired = true };

statusCommand.AddOption(notificationIdOption);
statusCommand.AddOption(apiUrlOption);

statusCommand.SetHandler(async (notificationId, apiUrl) =>
{
    try
    {
        Console.WriteLine($"🔍 Checking notification status...");
        Console.WriteLine($"   Notification ID: {notificationId}");

        using var httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var response = await httpClient.GetAsync($"/api/notifications/{notificationId}");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var status = result.GetProperty("status").GetString();
            var platform = result.GetProperty("targetPlatform").GetString();
            var retryCount = result.GetProperty("retryCount").GetInt32();
            var createdAt = result.GetProperty("createdAt").GetDateTime();

            Console.WriteLine($"   Status: {status}");
            Console.WriteLine($"   Platform: {platform}");
            Console.WriteLine($"   Retry Count: {retryCount}");
            Console.WriteLine($"   Created: {createdAt:u}");

            if (result.TryGetProperty("sentAt", out var sentAtProp) && sentAtProp.ValueKind != JsonValueKind.Null)
            {
                var sentAt = sentAtProp.GetDateTime();
                Console.WriteLine($"   Sent: {sentAt:u}");
            }

            if (result.TryGetProperty("errorMessage", out var errorProp) && errorProp.ValueKind != JsonValueKind.Null)
            {
                var error = errorProp.GetString();
                Console.WriteLine($"   Error: {error}");
            }
        }
        else
        {
            Console.WriteLine($"✗ Failed to get status: {response.StatusCode}");
            Environment.Exit(1);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error: {ex.Message}");
        Environment.Exit(1);
    }
}, notificationIdOption, apiUrlOption);

rootCommand.AddCommand(sendCommand);
rootCommand.AddCommand(statusCommand);

return await rootCommand.InvokeAsync(args);
