namespace Social_Media_Studio.Data.Entities;

public class PublishAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScheduleSlotId { get; set; }
    public ScheduleSlot? ScheduleSlot { get; set; }

    public string Platform { get; set; } = string.Empty;
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsSuccess { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ErrorMessage { get; set; }
}
