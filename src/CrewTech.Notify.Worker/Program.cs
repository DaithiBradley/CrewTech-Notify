using CrewTech.Notify.Core.Interfaces;
using CrewTech.Notify.Infrastructure.Data;
using CrewTech.Notify.Infrastructure.Providers;
using CrewTech.Notify.Infrastructure.Repositories;
using CrewTech.Notify.Worker;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;

var builder = Host.CreateApplicationBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=notifications.db";

builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    if (connectionString.Contains("Host=") || connectionString.Contains("Server="))
    {
        // PostgreSQL connection string
        options.UseNpgsql(connectionString);
    }
    else
    {
        // SQLite connection string
        options.UseSqlite(connectionString);
    }
});

// Repository
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Providers
builder.Services.AddHttpClient<WnsNotificationProvider>()
    .AddPolicyHandler(GetRetryPolicy());

builder.Services.AddHttpClient<FcmNotificationProvider>()
    .AddPolicyHandler(GetRetryPolicy());

builder.Services.AddSingleton<INotificationProvider, FakeNotificationProvider>();
builder.Services.AddSingleton<INotificationProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WnsNotificationProvider>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = new WnsConfiguration
    {
        ClientId = builder.Configuration["WNS:ClientId"] ?? "",
        ClientSecret = builder.Configuration["WNS:ClientSecret"] ?? "",
        TenantId = builder.Configuration["WNS:TenantId"] ?? ""
    };
    return new WnsNotificationProvider(logger, httpClient, config);
});

builder.Services.AddSingleton<INotificationProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FcmNotificationProvider>>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = new FcmConfiguration
    {
        ProjectId = builder.Configuration["FCM:ProjectId"] ?? "",
        ServerKey = builder.Configuration["FCM:ServerKey"] ?? ""
    };
    return new FcmNotificationProvider(logger, httpClient, config);
});

builder.Services.AddSingleton<NotificationProviderFactory>();

// Worker service
builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();

// Initialize database
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.EnsureCreated();
}

host.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx, 408
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            3, // Max 3 retries at HTTP level
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) 
                + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)), // Exponential backoff with random jitter
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                // Log retry attempts
            });
}
