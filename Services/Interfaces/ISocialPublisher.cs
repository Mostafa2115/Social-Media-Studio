namespace Social_Media_Studio.Services.Interfaces;

public class PublishRequest
{
    public Guid SlotId { get; set; }
    public Guid VariantId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class PublishResult
{
    public bool Success { get; set; }
    public string? PostUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawResponse { get; set; }

    public static PublishResult Succeeded(string? postUrl, string? rawResponse = null) =>
        new() { Success = true, PostUrl = postUrl, RawResponse = rawResponse };

    public static PublishResult Failed(string error, string? rawResponse = null) =>
        new() { Success = false, ErrorMessage = error, RawResponse = rawResponse };
}

public interface ISocialPublisher
{
    string PlatformName { get; }
    Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken cancellationToken = default);
}
