using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using CrewTech.Notify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using System.Net;

namespace CrewTech.Notify.Integration.Tests.Fixtures;

public class MultiContainerFixture : IAsyncLifetime
{
    private readonly INetwork _network;
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly IContainer _apiContainer;
    private readonly IContainer _workerContainer;

    public string ApiBaseUrl { get; private set; } = string.Empty;
    public string PostgresConnectionString => _postgresContainer.GetConnectionString();

    public MultiContainerFixture()
    {
        // Create shared Docker network
        _network = new NetworkBuilder()
            .WithName($"crewtech-test-{Guid.NewGuid():N}")
            .Build();

        // PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("crewtech_notify_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithNetwork(_network)
            .WithNetworkAliases("postgres")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        // Connection string for containers (using network alias)
        var containerConnectionString = "Host=postgres;Port=5432;Database=crewtech_notify_test;Username=testuser;Password=testpass";

        // API container
        _apiContainer = new ContainerBuilder()
            .WithImage("crewtech-notify-api:test")
            .WithNetwork(_network)
            .WithNetworkAliases("api")
            .WithPortBinding(8080, true)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("ConnectionStrings__DefaultConnection", containerConnectionString)
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/health")
                    .ForPort(8080)
                    .ForStatusCode(HttpStatusCode.OK)))
            .Build();

        // Worker container
        _workerContainer = new ContainerBuilder()
            .WithImage("crewtech-notify-worker:test")
            .WithNetwork(_network)
            .WithNetworkAliases("worker")
            .WithEnvironment("ConnectionStrings__DefaultConnection", containerConnectionString)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Notification Worker started"))
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start network
        await _network.CreateAsync();

        // Start database
        await _postgresContainer.StartAsync();

        // Apply migrations
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;
        
        await using var context = new NotificationDbContext(options);
        await context.Database.MigrateAsync();

        // Start API
        await _apiContainer.StartAsync();
        
        var apiPort = _apiContainer.GetMappedPublicPort(8080);
        ApiBaseUrl = $"http://localhost:{apiPort}";

        // Start Worker
        await _workerContainer.StartAsync();

        // Wait for system stabilization
        await Task.Delay(2000);
    }

    public async Task DisposeAsync()
    {
        await _workerContainer.DisposeAsync();
        await _apiContainer.DisposeAsync();
        await _postgresContainer.DisposeAsync();
        await _network.DeleteAsync();
    }

    public async Task<string> GetWorkerLogsAsync()
    {
        var (stdout, _) = await _workerContainer.GetLogsAsync();
        return stdout;
    }

    public async Task<string> GetApiLogsAsync()
    {
        var (stdout, _) = await _apiContainer.GetLogsAsync();
        return stdout;
    }
}
