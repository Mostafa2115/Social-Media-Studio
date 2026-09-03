using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Publishers;

public class MockLinkedInPublisher : ISocialPublisher
{
    private readonly ILogger<MockLinkedInPublisher> _logger;

    public string PlatformName => "LinkedIn";

    public MockLinkedInPublisher(ILogger<MockLinkedInPublisher> logger)
    {
        _logger = logger;
    }

    public Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockLinkedInPublisher: Publishing update for variant {VariantId}. IdempotencyKey: {Key}", 
            request.VariantId, request.IdempotencyKey);

        var shareUrn = $"urn:li:share:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{Random.Shared.Next(1000, 9999)}";
        var mockUrl = $"https://www.linkedin.com/feed/update/{shareUrn}";
        var preview = $"{{\"urn\":\"{shareUrn}\",\"text\":\"{request.Content.Replace("\"", "\\\"")}\",\"idempotency_key\":\"{request.IdempotencyKey}\"}}";

        return Task.FromResult(PublishResult.Succeeded(mockUrl, preview));
    }
}
