using CrewTech.Notify.Core.Interfaces;

namespace CrewTech.Notify.Infrastructure.Providers;

/// <summary>
/// Factory for getting notification providers by platform
/// </summary>
public class NotificationProviderFactory
{
    private readonly Dictionary<string, INotificationProvider> _providers;
    
    public NotificationProviderFactory(IEnumerable<INotificationProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Platform, StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Gets a provider for the specified platform
    /// </summary>
    public INotificationProvider? GetProvider(string platform)
    {
        _providers.TryGetValue(platform, out var provider);
        return provider;
    }
    
    /// <summary>
    /// Gets all registered providers
    /// </summary>
    public IEnumerable<INotificationProvider> GetAllProviders()
    {
        return _providers.Values;
    }
    
    /// <summary>
    /// Gets all supported platforms
    /// </summary>
    public IEnumerable<string> GetSupportedPlatforms()
    {
        return _providers.Keys;
    }
}
