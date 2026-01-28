using CrewTech.Notify.Core.Interfaces;
using CrewTech.Notify.Infrastructure.Data;
using CrewTech.Notify.Infrastructure.Providers;
using CrewTech.Notify.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "CrewTech Notify API", 
        Version = "v1",
        Description = "Enterprise-grade unified notifications platform"
    });
});

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

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
