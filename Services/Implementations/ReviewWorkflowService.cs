using Social_Media_Studio.Data;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Exceptions;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Implementations;

public class ReviewWorkflowService : IReviewWorkflowService
{
    private readonly AppDbContext _dbContext;
    private readonly IConstraintValidator _validator;
    private readonly ILogger<ReviewWorkflowService> _logger;

    public ReviewWorkflowService(AppDbContext dbContext, IConstraintValidator validator, ILogger<ReviewWorkflowService> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _logger = logger;
    }

    public async Task<PostVariant> ApproveVariantAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.PostVariants.FindAsync(new object[] { variantId }, cancellationToken);
        if (variant == null)
        {
            throw new KeyNotFoundException($"PostVariant with Id '{variantId}' not found.");
        }

        if (variant.Status == VariantStatus.Published)
        {
            throw new InvalidOperationException("Cannot change status of an already published variant.");
        }

        variant.Status = VariantStatus.Approved;
        variant.RejectionReason = null;
        variant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Variant {Id} for platform {Platform} approved.", variant.Id, variant.Platform);
        return variant;
    }

    public async Task<PostVariant> RejectVariantAsync(Guid variantId, RejectVariantRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.PostVariants.FindAsync(new object[] { variantId }, cancellationToken);
        if (variant == null)
        {
            throw new KeyNotFoundException($"PostVariant with Id '{variantId}' not found.");
        }

        if (variant.Status == VariantStatus.Published)
        {
            throw new InvalidOperationException("Cannot reject an already published variant.");
        }

        variant.Status = VariantStatus.Rejected;
        variant.RejectionReason = request.Reason;
        variant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Variant {Id} rejected with reason: {Reason}", variant.Id, request.Reason);
        return variant;
    }

    public async Task<PostVariant> EditVariantAsync(Guid variantId, EditVariantRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.PostVariants.FindAsync(new object[] { variantId }, cancellationToken);
        if (variant == null)
        {
            throw new KeyNotFoundException($"PostVariant with Id '{variantId}' not found.");
        }

        if (variant.Status == VariantStatus.Published)
        {
            throw new InvalidOperationException("Cannot edit an already published variant.");
        }

        var validation = _validator.Validate(variant.Platform, request.Content);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Edit rejected for variant {Id} on platform {Platform}: {Rule}", 
                variant.Id, variant.Platform, validation.BrokenRule);
            throw new ConstraintViolationException(variant.Platform, validation.BrokenRule!, validation.ErrorMessage!);
        }

        variant.Content = request.Content;
        variant.Status = VariantStatus.Draft;
        variant.RejectionReason = null;
        variant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Variant {Id} updated and reset to Draft for review.", variant.Id);
        return variant;
    }
}
