using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Social_Media_Studio.Data;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Implementations;

public class PostIngestionService : IPostIngestionService
{
    private readonly AppDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PostIngestionService> _logger;

    public PostIngestionService(AppDbContext dbContext, HttpClient httpClient, ILogger<PostIngestionService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BlogPost> IngestPostAsync(IngestPostRequest request, CancellationToken cancellationToken = default)
    {
        string title = request.Title ?? "Untitled Post";
        string content = request.Content ?? string.Empty;
        string? sourceUrl = request.Url;

        if (!string.IsNullOrWhiteSpace(sourceUrl) && string.IsNullOrWhiteSpace(content))
        {
            _logger.LogInformation("Fetching blog post content from URL: {Url}", sourceUrl);
            try
            {
                var html = await _httpClient.GetStringAsync(sourceUrl, cancellationToken);
                
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    var titleMatch = Regex.Match(html, @"<title>\s*(.+?)\s*</title>", RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        title = titleMatch.Groups[1].Value;
                    }
                }

                content = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"<[^>]+>", " ").Trim();
                content = Regex.Replace(content, @"\s{2,}", " ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch content from URL: {Url}", sourceUrl);
                throw new ArgumentException($"Failed to fetch content from URL '{sourceUrl}': {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Blog post content cannot be empty. Provide either 'content' or a valid 'url'.");
        }

        var post = new BlogPost
        {
            Id = Guid.NewGuid(),
            Title = title,
            Content = content,
            SourceUrl = sourceUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.BlogPosts.Add(post);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully ingested post {Id} ('{Title}')", post.Id, post.Title);
        return post;
    }

    public async Task<BlogPost?> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlogPosts
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<BlogPost>> GetAllPostsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BlogPosts
            .Include(p => p.Variants)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
