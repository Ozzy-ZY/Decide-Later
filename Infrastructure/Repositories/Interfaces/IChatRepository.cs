using Domain.Models;

namespace Infrastructure.Repositories.Interfaces;

public interface IChatRepository
{
    Task<Chat?> GetByIdAsync(Guid chatId, CancellationToken cancellationToken = default);
    Task<Chat?> GetPrivateChatBetweenUsersAsync(string userId1, string userId2, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Chat>> GetUserChatsAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(Chat chat, CancellationToken cancellationToken = default);
    Task<bool> IsUserInChatAsync(Guid chatId, string userId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

