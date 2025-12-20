using Domain.Models;
using Infrastructure.DataAccess.Db;
using Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _context;

    public MessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _context.Messages.AddAsync(message, cancellationToken);
        return message;
    }

    public Task<Message?> GetByIdWithSenderAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return _context.Messages
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Message> Items, int TotalCount)> GetMessagesPagedAsync(Guid chatId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Messages
            .Where(m => m.ChatId == chatId && !m.IsDeleted)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
