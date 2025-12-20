using Domain.Models;

namespace Infrastructure.Repositories.Interfaces;

public interface IMessageRepository
{
    Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default);
    Task<Message?> GetByIdWithSenderAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Message> Items, int TotalCount)> GetMessagesPagedAsync(Guid chatId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

