using System.Net.Http.Json;
using System.Text.Json;

namespace CrewTech.Notify.WindowsClient;

/// <summary>
/// Sample Windows client that demonstrates receiving notifications
/// In a real WNS scenario, this would register with WNS and receive push notifications
/// For demo purposes, this polls the API for notification status
/// </summary>
class Program
{
    private const string ApiUrl = "http://localhost:5000";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   CrewTech Notify - Windows Client    ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
        Console.WriteLine();
        
        if (args.Length > 0 && args[0] == "demo")
        {
            await RunDemoAsync();
        }
        else
        {
            await ShowInteractiveMenuAsync();
        }
    }
    
    static async Task ShowInteractiveMenuAsync()
    {
        while (true)
        {
            Console.WriteLine("\n📱 Windows Notification Client");
            Console.WriteLine("1. Send test notification");
            Console.WriteLine("2. Check notification status");
            Console.WriteLine("3. Run demo scenario");
            Console.WriteLine("4. Exit");
            Console.Write("\nSelect option: ");
            
            var choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    await SendTestNotificationAsync();
                    break;
                case "2":
                    await CheckNotificationStatusAsync();
                    break;
                case "3":
                    await RunDemoAsync();
                    break;
                case "4":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice");
                    break;
            }
        }
    }
    
    static async Task SendTestNotificationAsync()
    {
        Console.Write("\nEnter device token (or press Enter for 'test-device-001'): ");
        var deviceToken = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(deviceToken))
            deviceToken = "test-device-001";
        
        Console.Write("Enter title: ");
        var title = Console.ReadLine() ?? "Test Notification";
        
        Console.Write("Enter message: ");
        var body = Console.ReadLine() ?? "This is a test message";
        
        Console.Write("Platform (WNS/FCM/Fake, default Fake): ");
        var platform = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(platform))
            platform = "Fake";
        
        var request = new
        {
            targetPlatform = platform,
            deviceToken = deviceToken,
            title = title,
            body = body,
            tags = new[] { "test", "windows-client" },
            priority = "Normal"
        };
        
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(ApiUrl) };
            var response = await httpClient.PostAsJsonAsync("/api/notifications", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var notificationId = result.GetProperty("notificationId").GetGuid();
                Console.WriteLine($"\n✓ Notification sent successfully!");
                Console.WriteLine($"  ID: {notificationId}");
                Console.WriteLine($"  (Worker will process this shortly)");
            }
            else
            {
                Console.WriteLine($"\n✗ Failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error: {ex.Message}");
            Console.WriteLine("Make sure the API is running (dotnet run --project src/CrewTech.Notify.SenderApi)");
        }
    }
    
    static async Task CheckNotificationStatusAsync()
    {
        Console.Write("\nEnter notification ID: ");
        var idInput = Console.ReadLine();
        
        if (!Guid.TryParse(idInput, out var notificationId))
        {
            Console.WriteLine("Invalid notification ID");
            return;
        }
        
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(ApiUrl) };
            var response = await httpClient.GetAsync($"/api/notifications/{notificationId}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var status = result.GetProperty("status").GetString();
                var platform = result.GetProperty("targetPlatform").GetString();
                var retryCount = result.GetProperty("retryCount").GetInt32();
                
                Console.WriteLine($"\n📊 Notification Status");
                Console.WriteLine($"  ID: {notificationId}");
                Console.WriteLine($"  Status: {status}");
                Console.WriteLine($"  Platform: {platform}");
                Console.WriteLine($"  Retry Count: {retryCount}");
            }
            else
            {
                Console.WriteLine($"\n✗ Not found: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error: {ex.Message}");
        }
    }
    
    static async Task RunDemoAsync()
    {
        Console.WriteLine("\n🎬 Running demo scenario...\n");
        
        var scenarios = new[]
        {
            new { Title = "Welcome", Body = "Welcome to CrewTech Notify!", DeviceToken = "device-001", Platform = "Fake" },
            new { Title = "Update Available", Body = "A new update is available for your application", DeviceToken = "device-002", Platform = "Fake" },
            new { Title = "Task Completed", Body = "Your background task has completed successfully", DeviceToken = "device-003", Platform = "Fake" },
            new { Title = "Alert", Body = "Important: System maintenance scheduled", DeviceToken = "device-004", Platform = "Fake" },
        };
        
        var notificationIds = new List<Guid>();
        
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(ApiUrl) };
            
            foreach (var scenario in scenarios)
            {
                var request = new
                {
                    targetPlatform = scenario.Platform,
                    deviceToken = scenario.DeviceToken,
                    title = scenario.Title,
                    body = scenario.Body,
                    tags = new[] { "demo" },
                    priority = "Normal"
                };
                
                Console.WriteLine($"📤 Sending: {scenario.Title}");
                var response = await httpClient.PostAsJsonAsync("/api/notifications", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var notificationId = result.GetProperty("notificationId").GetGuid();
                    notificationIds.Add(notificationId);
                    Console.WriteLine($"   ✓ Queued with ID: {notificationId}");
                }
                
                await Task.Delay(500);
            }
            
            Console.WriteLine($"\n✓ Demo notifications queued! ({notificationIds.Count} total)");
            Console.WriteLine("The Worker service will process these notifications.");
            Console.WriteLine("Run the Worker if it's not already running:");
            Console.WriteLine("  dotnet run --project src/CrewTech.Notify.Worker\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error: {ex.Message}");
            Console.WriteLine("Make sure the API is running.");
        }
    }
}
