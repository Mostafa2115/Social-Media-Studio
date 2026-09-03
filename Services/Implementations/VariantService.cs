using Microsoft.EntityFrameworkCore;
using Social_Media_Studio.Data;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Exceptions;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Implementations;

public class VariantService : IVariantService
{
    private readonly AppDbContext _dbContext;
    private readonly IConstraintValidator _validator;
    private readonly ILogger<VariantService> _logger;

    public VariantService(AppDbContext dbContext, IConstraintValidator validator, ILogger<VariantService> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _logger = logger;
    }

    public async Task<List<PostVariant>> GenerateVariantsAsync(Guid blogPostId, CancellationToken cancellationToken = default)
    {
        var post = await _dbContext.BlogPosts.FindAsync(new object[] { blogPostId }, cancellationToken);
        if (post == null)
        {
            throw new KeyNotFoundException($"BlogPost with Id '{blogPostId}' not found.");
        }

        var platforms = new[] { "Telegram", "X", "LinkedIn" };
        var createdVariants = new List<PostVariant>();

        foreach (var platform in platforms)
        {
            // Compose content based on platform voice & rules
            string content = ComposePlatformContent(platform, post.Title, post.Content, post.SourceUrl);

            // Validate against constraint profile
            var validation = _validator.Validate(platform, content);
            if (!validation.IsValid)
            {
                _logger.LogError("Generated variant for {Platform} violated rule {Rule}: {Message}", 
                    platform, validation.BrokenRule, validation.ErrorMessage);
                throw new ConstraintViolationException(platform, validation.BrokenRule!, validation.ErrorMessage!);
            }

            var variant = new PostVariant
            {
                Id = Guid.NewGuid(),
                BlogPostId = post.Id,
                Platform = platform,
                Content = content,
                Status = VariantStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.PostVariants.Add(variant);
            createdVariants.Add(variant);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Generated {Count} valid variants for post {PostId}", createdVariants.Count, post.Id);
        return createdVariants;
    }

    public async Task<PostVariant> CreateCustomVariantAsync(Guid blogPostId, CreateCustomVariantRequest request, CancellationToken cancellationToken = default)
    {
        var post = await _dbContext.BlogPosts.FindAsync(new object[] { blogPostId }, cancellationToken);
        if (post == null)
        {
            throw new KeyNotFoundException($"BlogPost with Id '{blogPostId}' not found.");
        }

        // Validate constraint profile before creating
        var validation = _validator.Validate(request.Platform, request.Content);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Custom variant for {Platform} rejected: {Rule} - {Message}", 
                request.Platform, validation.BrokenRule, validation.ErrorMessage);
            throw new ConstraintViolationException(request.Platform, validation.BrokenRule!, validation.ErrorMessage!);
        }

        var variant = new PostVariant
        {
            Id = Guid.NewGuid(),
            BlogPostId = post.Id,
            Platform = request.Platform,
            Content = request.Content,
            Status = VariantStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PostVariants.Add(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return variant;
    }

    public async Task<PostVariant?> GetVariantByIdAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PostVariants
            .Include(v => v.BlogPost)
            .Include(v => v.ScheduleSlots)
            .FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken);
    }

    public async Task<List<PostVariant>> GetVariantsForPostAsync(Guid blogPostId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PostVariants
            .Where(v => v.BlogPostId == blogPostId)
            .OrderBy(v => v.Platform)
            .ToListAsync(cancellationToken);
    }

    private string ComposePlatformContent(string platform, string title, string rawContent, string? sourceUrl)
    {
        // Clean summary (first ~150-200 chars)
        var summary = rawContent.Length > 200 ? rawContent[..200].Trim() + "..." : rawContent.Trim();

        return platform.ToLowerInvariant() switch
        {
            "x" => ComposeXPost(title, summary, sourceUrl),
            "linkedin" => ComposeLinkedInPost(title, rawContent, sourceUrl),
            "telegram" => ComposeTelegramPost(title, summary, sourceUrl),
            _ => $"{title}\n\n{summary}\n\n#Update"
        };
    }

    private static string ComposeXPost(string title, string summary, string? sourceUrl)
    {
        // Max 280 chars, 1-3 hashtags, punchy
        var text = $"🚀 {title}\n\n{summary}";
        var tags = "\n\n#Tech #Update";
        var maxAllowedTextLen = 280 - tags.Length;
        if (text.Length > maxAllowedTextLen)
        {
            text = text[..(maxAllowedTextLen - 3)] + "...";
        }
        return $"{text}{tags}";
    }

    private static string ComposeLinkedInPost(string title, string rawContent, string? sourceUrl)
    {
        // Professional, key insights, 2-5 hashtags
        var snippet = rawContent.Length > 400 ? rawContent[..400].Trim() + "..." : rawContent.Trim();
        var urlLine = !string.IsNullOrWhiteSpace(sourceUrl) ? $"\n\nRead the full post here: {sourceUrl}" : "";
        return $"📌 {title}\n\nKey Takeaways & Insights:\n{snippet}{urlLine}\n\nWhat are your thoughts on this?\n\n#SoftwareEngineering #Backend #TechLeadership";
    }

    private static string ComposeTelegramPost(string title, string summary, string? sourceUrl)
    {
        // Informative, markdown formatted, clear call to action
        var urlLine = !string.IsNullOrWhiteSpace(sourceUrl) ? $"\n🔗 [Read More]({sourceUrl})" : "";
        return $"📢 *{title}*\n\n{summary}{urlLine}\n\n#Engineering #Newsletter";
    }
}
