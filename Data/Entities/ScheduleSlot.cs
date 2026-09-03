namespace Social_Media_Studio.Data.Entities;

public class ScheduleSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostVariantId { get; set; }
    public PostVariant? PostVariant { get; set; }

    public DateTime ScheduledTimeUtc { get; set; }
    public SlotStatus Status { get; set; } = SlotStatus.Pending;
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PublishAttempt> PublishAttempts { get; set; } = new List<PublishAttempt>();
}
