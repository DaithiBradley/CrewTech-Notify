using CrewTech.Notify.Core.Interfaces;
using CrewTech.Notify.Infrastructure.Data;
using CrewTech.Notify.Infrastructure.Providers;
using CrewTech.Notify.Infrastructure.Repositories;
using CrewTech.Notify.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=notifications.db"));

// Repository
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Providers
builder.Services.AddHttpClient<WnsNotificationProvider>();
builder.Services.AddHttpClient<FcmNotificationProvider>();

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
