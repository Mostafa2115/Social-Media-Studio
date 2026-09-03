using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Publishers;

public interface IPublisherResolver
{
    ISocialPublisher GetPublisher(string platform);
}

public class PublisherResolver : IPublisherResolver
{
    private readonly IEnumerable<ISocialPublisher> _publishers;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PublisherResolver> _logger;

    public PublisherResolver(
        IEnumerable<ISocialPublisher> publishers,
        IConfiguration configuration,
        ILogger<PublisherResolver> logger)
    {
        _publishers = publishers;
        _configuration = configuration;
        _logger = logger;
    }

    public ISocialPublisher GetPublisher(string platform)
    {
        // 1. Check if there is a global adapter override in configuration (e.g. for testing / Probe 6)
        var globalOverride = _configuration["Publishers:GlobalOverride"];
        if (!string.IsNullOrWhiteSpace(globalOverride))
        {
            var overridePub = _publishers.FirstOrDefault(p => 
                p.PlatformName.Equals(globalOverride, StringComparison.OrdinalIgnoreCase) ||
                p.GetType().Name.Equals(globalOverride, StringComparison.OrdinalIgnoreCase));

            if (overridePub != null)
            {
                _logger.LogInformation("Using global publisher override '{GlobalOverride}' for platform '{Platform}'", globalOverride, platform);
                return overridePub;
            }
        }

        // 2. Check platform-specific configuration mapping: e.g. "Publishers:Telegram" = "mock_x"
        var mappedTarget = _configuration[$"Publishers:{platform}"];
        var targetToLookup = !string.IsNullOrWhiteSpace(mappedTarget) ? mappedTarget : platform;
        var cleanTarget = targetToLookup.Replace("_", "").Trim();

        var publisher = _publishers.FirstOrDefault(p => 
            p.PlatformName.Equals(targetToLookup, StringComparison.OrdinalIgnoreCase) ||
            p.GetType().Name.Equals(targetToLookup, StringComparison.OrdinalIgnoreCase) ||
            p.GetType().Name.StartsWith(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
            p.PlatformName.Equals(cleanTarget.Replace("mock", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase));

        if (publisher == null)
        {
            // Fallback to matching by platform name directly
            publisher = _publishers.FirstOrDefault(p => p.PlatformName.Equals(platform, StringComparison.OrdinalIgnoreCase));
        }

        if (publisher == null)
        {
            throw new InvalidOperationException($"No publisher adapter found for platform '{platform}' (mapped: '{targetToLookup}').");
        }

        return publisher;
    }
}
