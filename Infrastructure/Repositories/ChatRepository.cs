using Application.Interfaces;
using Domain.Models;
using Infrastructure.DataAccess.Db;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly AppDbContext _context;

    public ChatRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Chat?> GetByIdAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        return _context.Chats
            .Include(c => c.Members)
            .ThenInclude(u => u.User)
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == chatId, cancellationToken);
    }

    public Task<Chat?> GetPrivateChatBetweenUsersAsync(string userId1, string userId2, CancellationToken cancellationToken = default)
    {
        var ordered = new[] { userId1, userId2 }.OrderBy(x => x).ToArray();
        var u1 = ordered[0];
        var u2 = ordered[1];

        return _context.Chats
            .Where(c => c.Type == ChatType.Direct)
            .Where(c => c.Members.Count == 2)
            .Where(c => c.Members.Any(m => m.UserId == u1) && c.Members.Any(m => m.UserId == u2))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Chat>> GetUserChatsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Chats
            .Where(c => c.Members.Any(m => m.UserId == userId && m.LeftAtUtc == null))
            .Include(c => c.Messages.OrderByDescending(m => m.SentAtUtc).Take(1))
            .Include(c => c.Members)
            .OrderByDescending(c => c.Messages.Max(m => (DateTime?)m.SentAtUtc) ?? c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Chat chat, CancellationToken cancellationToken = default)
    {
        await _context.Chats.AddAsync(chat, cancellationToken);
    }

    public Task<bool> IsUserInChatAsync(Guid chatId, string userId, CancellationToken cancellationToken = default)
    {
        return _context.ChatMembers.AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId && cm.LeftAtUtc == null, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
