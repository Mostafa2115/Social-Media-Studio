using Microsoft.EntityFrameworkCore;
using Social_Media_Studio.Data;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;
using Social_Media_Studio.Services.Publishers;

namespace Social_Media_Studio.Services.Implementations;

public class SchedulingService : ISchedulingService
{
    private readonly AppDbContext _dbContext;
    private readonly IPublisherResolver _publisherResolver;
    private readonly ILogger<SchedulingService> _logger;

    public SchedulingService(
        AppDbContext dbContext,
        IPublisherResolver publisherResolver,
        ILogger<SchedulingService> logger)
    {
        _dbContext = dbContext;
        _publisherResolver = publisherResolver;
        _logger = logger;
    }

    public async Task<ScheduleSlot> ScheduleVariantAsync(Guid variantId, ScheduleVariantRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.PostVariants.FindAsync(new object[] { variantId }, cancellationToken);
        if (variant == null)
        {
            throw new KeyNotFoundException($"PostVariant with Id '{variantId}' not found.");
        }

        if (variant.Status != VariantStatus.Approved)
        {
            _logger.LogWarning("Refused scheduling for variant {Id} with status {Status}", variant.Id, variant.Status);
            throw new InvalidOperationException($"Cannot schedule variant with status '{variant.Status}'. Only 'Approved' variants can be scheduled.");
        }

        var scheduledTime = request.ScheduledTimeUtc ?? DateTime.UtcNow;
        var idempotencyKey = $"var_{variant.Id}_{scheduledTime:yyyyMMddHHmmss}";

        var existingSlot = await _dbContext.ScheduleSlots
            .Include(s => s.PostVariant)
            .FirstOrDefaultAsync(s => s.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingSlot != null)
        {
            _logger.LogInformation("Returning existing schedule slot {SlotId} for idempotency key {Key}", existingSlot.Id, idempotencyKey);
            return existingSlot;
        }

        var slot = new ScheduleSlot
        {
            Id = Guid.NewGuid(),
            PostVariantId = variant.Id,
            ScheduledTimeUtc = scheduledTime,
            Status = SlotStatus.Pending,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ScheduleSlots.Add(slot);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scheduled variant {VariantId} for {Time} with IdempotencyKey: {Key}", 
            variant.Id, scheduledTime, idempotencyKey);

        return slot;
    }

    public async Task<ScheduleSlot?> GetSlotByIdAsync(Guid slotId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduleSlots
            .Include(s => s.PostVariant)
            .Include(s => s.PublishAttempts)
            .FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken);
    }

    public async Task<List<ScheduleSlot>> GetAllSlotsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduleSlots
            .Include(s => s.PostVariant)
            .Include(s => s.PublishAttempts)
            .OrderByDescending(s => s.ScheduledTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PublishAttempt>> GetPublishHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.PublishAttempts
            .Include(p => p.ScheduleSlot)
            .OrderByDescending(p => p.AttemptedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ProcessDueSlotsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueSlots = await _dbContext.ScheduleSlots
            .Include(s => s.PostVariant)
            .Include(s => s.PublishAttempts)
            .Where(s => (s.Status == SlotStatus.Pending || s.Status == SlotStatus.Processing) && s.ScheduledTimeUtc <= now)
            .ToListAsync(cancellationToken);

        int processedCount = 0;

        foreach (var slot in dueSlots)
        {
            if (slot.PostVariant == null)
            {
                continue;
            }

            var existingSuccess = slot.PublishAttempts.FirstOrDefault(a => a.IsSuccess);
            if (existingSuccess != null)
            {
                _logger.LogInformation("Slot {SlotId} already has a successful publish attempt. Marking Completed without re-publishing.", slot.Id);
                slot.Status = SlotStatus.Completed;
                slot.PostVariant.Status = VariantStatus.Published;
                await _dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            slot.Status = SlotStatus.Processing;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var publisher = _publisherResolver.GetPublisher(slot.PostVariant.Platform);

            var publishRequest = new PublishRequest
            {
                SlotId = slot.Id,
                VariantId = slot.PostVariant.Id,
                Platform = slot.PostVariant.Platform,
                Content = slot.PostVariant.Content,
                IdempotencyKey = slot.IdempotencyKey
            };

            PublishResult result;
            try
            {
                result = await publisher.PublishAsync(publishRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while publishing slot {SlotId}", slot.Id);
                result = PublishResult.Failed($"Unexpected error: {ex.Message}");
            }

            var attempt = new PublishAttempt
            {
                Id = Guid.NewGuid(),
                ScheduleSlotId = slot.Id,
                Platform = slot.PostVariant.Platform,
                AttemptedAtUtc = DateTime.UtcNow,
                IsSuccess = result.Success,
                ResponsePayload = result.Success ? result.PostUrl : result.RawResponse,
                ErrorMessage = result.ErrorMessage
            };

            _dbContext.PublishAttempts.Add(attempt);

            if (result.Success)
            {
                slot.Status = SlotStatus.Completed;
                slot.PostVariant.Status = VariantStatus.Published;
                _logger.LogInformation("Slot {SlotId} published successfully via {Platform}. Live URL: {Url}", 
                    slot.Id, slot.PostVariant.Platform, result.PostUrl);
            }
            else
            {
                slot.Status = SlotStatus.Failed;
                _logger.LogWarning("Slot {SlotId} publishing failed via {Platform}: {Error}", 
                    slot.Id, slot.PostVariant.Platform, result.ErrorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            processedCount++;
        }

        return processedCount;
    }
}
