using Application.DTOs.Chat;
using Application.Interfaces;
using Domain.Models;
using Application.Exceptions;
using Infrastructure.Repositories.Interfaces;

namespace Application.Services;

public class MessageService(IChatRepository chatRepository, IMessageRepository messageRepository)
    : IMessageService
{
    public async Task<MessageDto> SendMessageAsync(string currentUserId, Guid chatId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new MessageValidationException("Message content is required", nameof(content));
        }

        var isMember = await chatRepository.IsUserInChatAsync(chatId, currentUserId, cancellationToken);
        if (!isMember)
        {
            throw new MessageUnauthorizedException("You are not a member of this chat.");
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = currentUserId,
            Content = content,
            SentAtUtc = DateTime.UtcNow
        };

        await messageRepository.AddAsync(message, cancellationToken);
        await messageRepository.SaveChangesAsync(cancellationToken);
        var saved = await messageRepository.GetByIdWithSenderAsync(message.Id, cancellationToken);
        if (saved is null)
        {
            throw new MessageNotFoundException("Message was not found after saving.");
        }

        return new MessageDto
        {
            Id = saved.Id,
            ChatId = saved.ChatId,
            SenderUserName = saved.Sender?.UserName ?? "Hanz_Error",
            Content = saved.Content,
            SentAtUtc = saved.SentAtUtc
        };
    }

    public async Task<PagedResultDto<MessageDto>> GetMessagesAsync(string currentUserId, Guid chatId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 50;

        var isMember = await chatRepository.IsUserInChatAsync(chatId, currentUserId, cancellationToken);
        if (!isMember)
        {
            throw new MessageUnauthorizedException("You are not a member of this chat.");
        }

        var (items, totalCount) = await messageRepository.GetMessagesPagedAsync(chatId, pageNumber, pageSize, cancellationToken);

        var messageDtos = items
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ChatId = m.ChatId,
                SenderUserName = m.Sender!.UserName!, // must be non-null because of the join
                Content = m.Content,
                SentAtUtc = m.SentAtUtc
            })
            .ToList();

        return new PagedResultDto<MessageDto>
        {
            Items = messageDtos,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
