using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Publishers;

public class MockXPublisher : ISocialPublisher
{
    private readonly ILogger<MockXPublisher> _logger;

    public string PlatformName => "X";

    public MockXPublisher(ILogger<MockXPublisher> logger)
    {
        _logger = logger;
    }

    public Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockXPublisher: Publishing post for variant {VariantId}. IdempotencyKey: {Key}", 
            request.VariantId, request.IdempotencyKey);

        var tweetId = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{Random.Shared.Next(100, 999)}";
        var mockUrl = $"https://x.com/mock_user/status/{tweetId}";
        var preview = $"{{\"tweet_id\":\"{tweetId}\",\"content\":\"{request.Content.Replace("\"", "\\\"")}\",\"idempotency_key\":\"{request.IdempotencyKey}\"}}";

        return Task.FromResult(PublishResult.Succeeded(mockUrl, preview));
    }
}
