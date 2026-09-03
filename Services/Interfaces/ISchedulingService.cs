using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;

namespace Social_Media_Studio.Services.Interfaces;

public interface ISchedulingService
{
    Task<ScheduleSlot> ScheduleVariantAsync(Guid variantId, ScheduleVariantRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleSlot?> GetSlotByIdAsync(Guid slotId, CancellationToken cancellationToken = default);
    Task<List<ScheduleSlot>> GetAllSlotsAsync(CancellationToken cancellationToken = default);
    Task<List<PublishAttempt>> GetPublishHistoryAsync(CancellationToken cancellationToken = default);
    Task<int> ProcessDueSlotsAsync(CancellationToken cancellationToken = default);
}
