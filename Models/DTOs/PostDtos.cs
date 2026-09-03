using System.ComponentModel.DataAnnotations;
using Social_Media_Studio.Data.Entities;

namespace Social_Media_Studio.Models.DTOs;

public class IngestPostRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
}

public class BlogPostResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<PostVariantResponse> Variants { get; set; } = new();
}

public class PostVariantResponse
{
    public Guid Id { get; set; }
    public Guid BlogPostId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public VariantStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class CreateCustomVariantRequest
{
    [Required]
    public string Platform { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}

public class EditVariantRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}

public class RejectVariantRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class ScheduleVariantRequest
{
    public DateTime? ScheduledTimeUtc { get; set; }
}

public class ScheduleSlotResponse
{
    public Guid Id { get; set; }
    public Guid PostVariantId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateTime ScheduledTimeUtc { get; set; }
    public SlotStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<PublishAttemptResponse> Attempts { get; set; } = new();
}

public class PublishAttemptResponse
{
    public Guid Id { get; set; }
    public Guid ScheduleSlotId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateTime AttemptedAtUtc { get; set; }
    public bool IsSuccess { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ErrorMessage { get; set; }
}
