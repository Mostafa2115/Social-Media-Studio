using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Models.DTOs;

namespace Social_Media_Studio.Services.Interfaces;

public interface IPostIngestionService
{
    Task<BlogPost> IngestPostAsync(IngestPostRequest request, CancellationToken cancellationToken = default);
    Task<BlogPost?> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<BlogPost>> GetAllPostsAsync(CancellationToken cancellationToken = default);
}
