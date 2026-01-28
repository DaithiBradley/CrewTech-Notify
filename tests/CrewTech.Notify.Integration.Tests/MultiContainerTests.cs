using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CrewTech.Notify.Core.Entities;
using CrewTech.Notify.Infrastructure.Data;
using CrewTech.Notify.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace CrewTech.Notify.Integration.Tests;

[Collection("MultiContainer")]
public class MultiContainerTests : IClassFixture<MultiContainerFixture>
{
    private readonly MultiContainerFixture _fixture;
    private readonly HttpClient _httpClient;
    private readonly ITestOutputHelper _output;

    public MultiContainerTests(MultiContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _httpClient = new HttpClient { BaseAddress = new Uri(_fixture.ApiBaseUrl) };
    }

    [Fact]
    public async Task FullE2E_SendNotification_WorkerProcesses_DatabaseUpdates()
    {
        // ARRANGE
        var notificationRequest = new
        {
            idempotencyKey = Guid.NewGuid().ToString(),
            targetPlatform = "Fake",
            deviceToken = "test-device-e2e",
            title = "E2E Test Notification",
            body = "Testing full container communication",
            tags = new[] { "e2e", "testcontainers" },
            priority = "High"
        };

        // ACT 1: Send to API container
        _output.WriteLine("Step 1: Sending notification to API...");
        var response = await _httpClient.PostAsJsonAsync("/api/notifications", notificationRequest);
        
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var notificationId = result.GetProperty("notificationId").GetGuid();
        _output.WriteLine($"✓ Notification queued: {notificationId}");

        // ACT 2: Wait for Worker to process
        _output.WriteLine("Step 2: Waiting for Worker to process...");
        await Task.Delay(8000);

        // ACT 3: Query status
        _output.WriteLine("Step 3: Checking notification status...");
        var statusResponse = await _httpClient.GetAsync($"/api/notifications/{notificationId}");
        Assert.True(statusResponse.IsSuccessStatusCode);

        var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        var currentStatus = status.GetProperty("status").GetString();
        var retryCount = status.GetProperty("retryCount").GetInt32();

        // ASSERT
        _output.WriteLine($"Final status: {currentStatus}, Retries: {retryCount}");
        Assert.Equal("Sent", currentStatus);

        // Verify in database
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(_fixture.PostgresConnectionString)
            .Options;
        
        await using var context = new NotificationDbContext(options);
        var dbNotification = await context.NotificationMessages.FindAsync(notificationId);
        
        Assert.NotNull(dbNotification);
        Assert.Equal(NotificationStatus.Sent, dbNotification.Status);
        Assert.NotNull(dbNotification.SentAt);

        _output.WriteLine("\n--- Worker Logs ---");
        _output.WriteLine(await _fixture.GetWorkerLogsAsync());
    }

    [Fact]
    public async Task ConcurrentRequests_MultipleContainers_NoRaceConditions()
    {
        var tasks = Enumerable.Range(1, 20).Select(async i =>
        {
            var request = new
            {
                idempotencyKey = $"concurrent-{Guid.NewGuid()}",
                targetPlatform = "Fake",
                deviceToken = $"device-{i}",
                title = $"Concurrent Test {i}",
                body = "Testing concurrency"
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notifications", request);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        });

        var results = await Task.WhenAll(tasks);
        var ids = results.Select(r => r.GetProperty("notificationId").GetGuid()).ToList();
        Assert.Equal(20, ids.Distinct().Count());

        await Task.Delay(12000);

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(_fixture.PostgresConnectionString)
            .Options;
        
        await using var context = new NotificationDbContext(options);
        var processed = await context.NotificationMessages
            .Where(n => ids.Contains(n.Id))
            .CountAsync();

        Assert.Equal(20, processed);
    }

    [Fact]
    public async Task IdempotencyAcrossContainers_DuplicateRequest_Returns409()
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new
        {
            idempotencyKey,
            targetPlatform = "Fake",
            deviceToken = "test-device",
            title = "Idempotency Test",
            body = "Testing duplicate detection"
        };

        var response1 = await _httpClient.PostAsJsonAsync("/api/notifications", request);
        Assert.Equal(HttpStatusCode.Accepted, response1.StatusCode);

        var response2 = await _httpClient.PostAsJsonAsync("/api/notifications", request);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);

        var result = await response2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("already exists", result.GetProperty("message").GetString());
    }

    [Fact]
    public async Task WorkerRetryLogic_EventuallySucceeds()
    {
        var notificationIds = new List<Guid>();
        
        for (int i = 0; i < 50; i++)
        {
            var request = new
            {
                targetPlatform = "Fake",
                deviceToken = $"retry-test-{i}",
                title = $"Retry Test {i}",
                body = "Testing retry logic"
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notifications", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                notificationIds.Add(result.GetProperty("notificationId").GetGuid());
            }
        }

        await Task.Delay(25000);

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(_fixture.PostgresConnectionString)
            .Options;
        
        await using var context = new NotificationDbContext(options);
        var stats = await context.NotificationMessages
            .Where(n => notificationIds.Contains(n.Id))
            .GroupBy(n => 1)
            .Select(g => new
            {
                TotalSent = g.Count(n => n.Status == NotificationStatus.Sent),
                AverageRetries = g.Average(n => n.RetryCount),
                MaxRetries = g.Max(n => n.RetryCount)
            })
            .FirstOrDefaultAsync();

        Assert.NotNull(stats);
        _output.WriteLine($"Stats: Sent={stats.TotalSent}, Avg Retries={stats.AverageRetries:F2}, Max={stats.MaxRetries}");
        Assert.True(stats.TotalSent > 45);
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _httpClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", health.GetProperty("status").GetString());
    }
}
