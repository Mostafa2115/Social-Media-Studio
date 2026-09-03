using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;

namespace Social_Media_Studio.Services.Interfaces;

public interface IReviewWorkflowService
{
    Task<PostVariant> ApproveVariantAsync(Guid variantId, CancellationToken cancellationToken = default);
    Task<PostVariant> RejectVariantAsync(Guid variantId, RejectVariantRequest request, CancellationToken cancellationToken = default);
    Task<PostVariant> EditVariantAsync(Guid variantId, EditVariantRequest request, CancellationToken cancellationToken = default);
}
