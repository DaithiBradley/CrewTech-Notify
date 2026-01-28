using Microsoft.Extensions.Configuration;

namespace CrewTech.Notify.Infrastructure.Tests.Helpers;

public static class TestConfiguration
{
    private static IConfiguration? _configuration;
    
    public static IConfiguration Configuration
    {
        get
        {
            if (_configuration == null)
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false)
                    .AddJsonFile("appsettings.Test.Local.json", optional: true, reloadOnChange: false)
                    .AddUserSecrets(typeof(TestConfiguration).Assembly, optional: true)
                    .AddEnvironmentVariables(prefix: "CREWTECH_TEST_");
                
                _configuration = builder.Build();
            }
            return _configuration;
        }
    }
    
    public static bool UseRealCredentials
    {
        get
        {
            var value = Configuration["TestConfiguration:UseRealCredentials"];
            return !string.IsNullOrEmpty(value) && bool.Parse(value);
        }
    }
    
    public static string? WnsClientId => Configuration["WNS:ClientId"];
    public static string? WnsClientSecret => Configuration["WNS:ClientSecret"];
    public static string? WnsTenantId => Configuration["WNS:TenantId"];
    public static string? FcmProjectId => Configuration["FCM:ProjectId"];
    public static string? FcmServerKey => Configuration["FCM:ServerKey"];
    public static string? WnsDeviceToken => Configuration["TestDevices:WNS:DeviceToken"];
    public static string? FcmDeviceToken => Configuration["TestDevices:FCM:DeviceToken"];
    public static string ApiBaseUrl => Configuration["TestConfiguration:ApiBaseUrl"] ?? "http://localhost:5000";
}
