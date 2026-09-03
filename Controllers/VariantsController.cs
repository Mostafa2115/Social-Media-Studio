using Microsoft.AspNetCore.Mvc;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Exceptions;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VariantsController : ControllerBase
{
    private readonly IVariantService _variantService;
    private readonly IReviewWorkflowService _reviewService;
    private readonly ISchedulingService _schedulingService;

    public VariantsController(
        IVariantService variantService,
        IReviewWorkflowService reviewService,
        ISchedulingService schedulingService)
    {
        _variantService = variantService;
        _reviewService = reviewService;
        _schedulingService = schedulingService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostVariantResponse>> GetVariantById(Guid id)
    {
        var variant = await _variantService.GetVariantByIdAsync(id);
        if (variant == null)
        {
            return NotFound(new { error = $"PostVariant with Id '{id}' was not found." });
        }
        return Ok(MapToVariantResponse(variant));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<PostVariantResponse>> ApproveVariant(Guid id)
    {
        try
        {
            var variant = await _reviewService.ApproveVariantAsync(id);
            return Ok(MapToVariantResponse(variant));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<PostVariantResponse>> RejectVariant(Guid id, [FromBody] RejectVariantRequest request)
    {
        try
        {
            var variant = await _reviewService.RejectVariantAsync(id, request);
            return Ok(MapToVariantResponse(variant));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostVariantResponse>> EditVariant(Guid id, [FromBody] EditVariantRequest request)
    {
        try
        {
            var variant = await _reviewService.EditVariantAsync(id, request);
            return Ok(MapToVariantResponse(variant));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ConstraintViolationException ex)
        {
            return BadRequest(new 
            { 
                error = "Constraint profile validation failed on edited content.",
                platform = ex.Platform,
                brokenRule = ex.BrokenRule,
                message = ex.Message 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/schedule")]
    public async Task<ActionResult<ScheduleSlotResponse>> ScheduleVariant(Guid id, [FromBody] ScheduleVariantRequest? request)
    {
        try
        {
            var slot = await _schedulingService.ScheduleVariantAsync(id, request ?? new ScheduleVariantRequest());
            return Ok(MapToSlotResponse(slot));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

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

    private static ScheduleSlotResponse MapToSlotResponse(ScheduleSlot slot) => new()
    {
        Id = slot.Id,
        PostVariantId = slot.PostVariantId,
        Platform = slot.PostVariant?.Platform ?? "Unknown",
        ScheduledTimeUtc = slot.ScheduledTimeUtc,
        Status = slot.Status,
        IdempotencyKey = slot.IdempotencyKey,
        CreatedAtUtc = slot.CreatedAtUtc
    };
}
