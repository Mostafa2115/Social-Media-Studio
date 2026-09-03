using Microsoft.AspNetCore.Mvc;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;

    public ScheduleController(ISchedulingService schedulingService)
    {
        _schedulingService = schedulingService;
    }

    [HttpGet("slots")]
    public async Task<ActionResult<List<ScheduleSlotResponse>>> GetAllSlots()
    {
        var slots = await _schedulingService.GetAllSlotsAsync();
        return Ok(slots.Select(MapToSlotResponse).ToList());
    }

    [HttpGet("slots/{id:guid}")]
    public async Task<ActionResult<ScheduleSlotResponse>> GetSlotById(Guid id)
    {
        var slot = await _schedulingService.GetSlotByIdAsync(id);
        if (slot == null)
        {
            return NotFound(new { error = $"ScheduleSlot with Id '{id}' not found." });
        }
        return Ok(MapToSlotResponse(slot));
    }

    [HttpPost("process-due")]
    public async Task<ActionResult> ProcessDueSlots()
    {
        int processed = await _schedulingService.ProcessDueSlotsAsync();
        return Ok(new { message = $"Processed {processed} due slots.", processedCount = processed });
    }

    private static ScheduleSlotResponse MapToSlotResponse(ScheduleSlot slot) => new()
    {
        Id = slot.Id,
        PostVariantId = slot.PostVariantId,
        Platform = slot.PostVariant?.Platform ?? "Unknown",
        ScheduledTimeUtc = slot.ScheduledTimeUtc,
        Status = slot.Status,
        IdempotencyKey = slot.IdempotencyKey,
        CreatedAtUtc = slot.CreatedAtUtc,
        Attempts = slot.PublishAttempts.Select(a => new PublishAttemptResponse
        {
            Id = a.Id,
            ScheduleSlotId = a.ScheduleSlotId,
            Platform = a.Platform,
            AttemptedAtUtc = a.AttemptedAtUtc,
            IsSuccess = a.IsSuccess,
            ResponsePayload = a.ResponsePayload,
            ErrorMessage = a.ErrorMessage
        }).ToList()
    };
}
