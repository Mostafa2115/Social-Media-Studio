namespace Social_Media_Studio.Data.Entities;

public class PostVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlogPostId { get; set; }
    public BlogPost? BlogPost { get; set; }

    public string Platform { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public VariantStatus Status { get; set; } = VariantStatus.Draft;
    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ScheduleSlot> ScheduleSlots { get; set; } = new List<ScheduleSlot>();
}
