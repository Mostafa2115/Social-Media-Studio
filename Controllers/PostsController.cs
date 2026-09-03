using Microsoft.AspNetCore.Mvc;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Exceptions;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly IPostIngestionService _ingestionService;
    private readonly IVariantService _variantService;

    public PostsController(IPostIngestionService ingestionService, IVariantService variantService)
    {
        _ingestionService = ingestionService;
        _variantService = variantService;
    }

    
    [HttpPost]
    public async Task<ActionResult<BlogPostResponse>> IngestPost([FromBody] IngestPostRequest request)
    {
        try
        {
            var post = await _ingestionService.IngestPostAsync(request);
            return CreatedAtAction(nameof(GetPostById), new { id = post.Id }, MapToResponse(post));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    
    [HttpGet]
    public async Task<ActionResult<List<BlogPostResponse>>> GetAllPosts()
    {
        var posts = await _ingestionService.GetAllPostsAsync();
        return Ok(posts.Select(MapToResponse).ToList());
    }

    
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlogPostResponse>> GetPostById(Guid id)
    {
        var post = await _ingestionService.GetPostByIdAsync(id);
        if (post == null)
        {
            return NotFound(new { error = $"BlogPost with Id '{id}' was not found." });
        }
        return Ok(MapToResponse(post));
    }

    
    [HttpPost("{id:guid}/generate-variants")]
    public async Task<ActionResult<List<PostVariantResponse>>> GenerateVariants(Guid id)
    {
        try
        {
            var variants = await _variantService.GenerateVariantsAsync(id);
            return Ok(variants.Select(MapToVariantResponse).ToList());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ConstraintViolationException ex)
        {
            return BadRequest(new 
            { 
                error = "Variant generation violated constraint profile.",
                platform = ex.Platform,
                brokenRule = ex.BrokenRule,
                message = ex.Message 
            });
        }
    }

    [HttpPost("{id:guid}/variants")]
    public async Task<ActionResult<PostVariantResponse>> CreateCustomVariant(Guid id, [FromBody] CreateCustomVariantRequest request)
    {
        try
        {
            var variant = await _variantService.CreateCustomVariantAsync(id, request);
            return CreatedAtAction("GetVariantById", "Variants", new { id = variant.Id }, MapToVariantResponse(variant));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ConstraintViolationException ex)
        {
            return BadRequest(new 
            { 
                error = "Constraint profile validation failed.",
                platform = ex.Platform,
                brokenRule = ex.BrokenRule,
                message = ex.Message 
            });
        }
    }

    [HttpGet("{id:guid}/variants")]
    public async Task<ActionResult<List<PostVariantResponse>>> GetVariantsForPost(Guid id)
    {
        var variants = await _variantService.GetVariantsForPostAsync(id);
        return Ok(variants.Select(MapToVariantResponse).ToList());
    }

    private static BlogPostResponse MapToResponse(BlogPost post) => new()
    {
        Id = post.Id,
        Title = post.Title,
        Content = post.Content,
        SourceUrl = post.SourceUrl,
        CreatedAtUtc = post.CreatedAtUtc,
        Variants = post.Variants.Select(MapToVariantResponse).ToList()
    };

    private static PostVariantResponse MapToVariantResponse(PostVariant variant) => new()
    {
        Id = variant.Id,
        BlogPostId = variant.BlogPostId,
        Platform = variant.Platform,
        Content = variant.Content,
        Status = variant.Status,
        RejectionReason = variant.RejectionReason,
        CreatedAtUtc = variant.CreatedAtUtc,
        UpdatedAtUtc = variant.UpdatedAtUtc
    };
}
