using Microsoft.AspNetCore.Mvc;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HistoryController : ControllerBase
{
    private readonly ISchedulingService _schedulingService;

    public HistoryController(ISchedulingService schedulingService)
    {
        _schedulingService = schedulingService;
    }

    /// <summary>
    /// View full publish attempts history
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PublishAttemptResponse>>> GetHistory()
    {
        var history = await _schedulingService.GetPublishHistoryAsync();
        var response = history.Select(a => new PublishAttemptResponse
        {
            Id = a.Id,
            ScheduleSlotId = a.ScheduleSlotId,
            Platform = a.Platform,
            AttemptedAtUtc = a.AttemptedAtUtc,
            IsSuccess = a.IsSuccess,
            ResponsePayload = a.ResponsePayload,
            ErrorMessage = a.ErrorMessage
        }).ToList();

        return Ok(response);
    }
}
