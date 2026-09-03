using System.Text;
using System.Text.Json;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Publishers;

public class TelegramPublisher : ISocialPublisher
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramPublisher> _logger;

    public string PlatformName => "Telegram";

    public TelegramPublisher(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramPublisher> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken cancellationToken = default)
    {
        var botToken = _configuration["Telegram:BotToken"];
        var chatId = _configuration["Telegram:ChatId"];

        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId) || botToken.Contains("YOUR_BOT_TOKEN"))
        {
            _logger.LogWarning("Telegram credentials not configured. Simulating successful Telegram publish.");
            var simulatedMessageId = Random.Shared.Next(1000, 99999);
            return PublishResult.Succeeded(
                $"https://t.me/c/simulated_channel/{simulatedMessageId}",
                $"[Simulated] Token not configured. Content: {request.Content}"
            );
        }

        try
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = request.Content,
                parse_mode = "Markdown"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, jsonContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to publish to Telegram. Status: {StatusCode}, Body: {Body}", response.StatusCode, responseBody);
                return PublishResult.Failed($"Telegram API Error ({(int)response.StatusCode}): {responseBody}", responseBody);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            long messageId = 0;
            if (root.TryGetProperty("result", out var resultElement) && resultElement.TryGetProperty("message_id", out var msgIdElement))
            {
                messageId = msgIdElement.GetInt64();
            }

            // If public channel with username e.g. @mychannel -> https://t.me/mychannel/123
            var channelUsername = chatId.TrimStart('@');
            var messageUrl = chatId.StartsWith("@")
                ? $"https://t.me/{channelUsername}/{messageId}"
                : $"https://t.me/c/{chatId.TrimStart('-')}/{messageId}";

            return PublishResult.Succeeded(messageUrl, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while publishing to Telegram");
            return PublishResult.Failed(ex.Message);
        }
    }
}
