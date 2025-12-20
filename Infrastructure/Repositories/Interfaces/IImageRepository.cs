using Domain.Models;

namespace Infrastructure.Repositories.Interfaces;

public interface IImageRepository
{
    Task<Image?> GetByIdAsync(Guid imageId, CancellationToken cancellationToken = default);
    Task<Image?> GetProfilePictureByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Image>> GetImagesByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(Image image, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid imageId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

