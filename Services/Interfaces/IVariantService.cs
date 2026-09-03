using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;

namespace Social_Media_Studio.Services.Interfaces;

public interface IVariantService
{
    Task<List<PostVariant>> GenerateVariantsAsync(Guid blogPostId, CancellationToken cancellationToken = default);
    Task<PostVariant> CreateCustomVariantAsync(Guid blogPostId, CreateCustomVariantRequest request, CancellationToken cancellationToken = default);
    Task<PostVariant?> GetVariantByIdAsync(Guid variantId, CancellationToken cancellationToken = default);
    Task<List<PostVariant>> GetVariantsForPostAsync(Guid blogPostId, CancellationToken cancellationToken = default);
}
