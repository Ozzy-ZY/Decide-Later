using Application.DTOs.Chat;
using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Application.Exceptions;

namespace Application.Services;

public class ChatService(IChatRepository chatRepository, UserManager<ApplicationUser> userManager)
    : IChatService
{
    public async Task<ChatDto> CreatePrivateChatAsync(string currentUserId, string targetUserName, CancellationToken cancellationToken = default)
    {
        var currentUser = await userManager.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
                          ?? throw new ChatOperationException("Current user not found");

        var targetUser = await userManager.FindByNameAsync(targetUserName)
                         ?? throw new ChatNotFoundException("Target user not found");

        if (targetUser.Id == currentUserId)
        {
            throw new ChatOperationException("Cannot create a private chat with yourself.");
        }

        var existingChat = await chatRepository.GetPrivateChatBetweenUsersAsync(currentUserId, targetUser.Id, cancellationToken);
        if (existingChat != null)
        {
            return MapToChatDto(existingChat);
        }

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = ChatType.Direct,
            Name = $"{currentUser.UserName} & {targetUser.UserName}", // the client is responsible for displaying appropriate names
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        chat.Members.Add(new ChatMember
        {
            ChatId = chat.Id,
            UserId = currentUserId,
            IsOwner = true,
            IsAdmin = true,
            JoinedAtUtc = DateTime.UtcNow
        });

        chat.Members.Add(new ChatMember
        {
            ChatId = chat.Id,
            UserId = targetUser.Id,
            JoinedAtUtc = DateTime.UtcNow
        });

        await chatRepository.AddAsync(chat, cancellationToken);
        await chatRepository.SaveChangesAsync(cancellationToken);

        return MapToChatDto(chat);
    }

    public async Task<ChatDto> CreateGroupChatAsync(string currentUserId, string name, IEnumerable<string> initialMemberUserNames, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ChatOperationException("Group name is required");
        }

        var creator = await userManager.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
                      ?? throw new ChatOperationException("Current user not found");

        var usernames = initialMemberUserNames?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

        var members = new List<ApplicationUser>();
        foreach (var username in usernames)
        {
            var user = await userManager.FindByNameAsync(username);
            if (user != null)
            {
                members.Add(user);
            }
        }

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = ChatType.Group,
            Name = name,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        chat.Members.Add(new ChatMember
        {
            ChatId = chat.Id,
            UserId = currentUserId,
            IsOwner = true,
            IsAdmin = true,
            JoinedAtUtc = DateTime.UtcNow
        });

        foreach (var member in members.Where(m => m.Id != currentUserId))
        {
            chat.Members.Add(new ChatMember
            {
                ChatId = chat.Id,
                UserId = member.Id,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        await chatRepository.AddAsync(chat, cancellationToken);
        await chatRepository.SaveChangesAsync(cancellationToken);

        return MapToChatDto(chat);
    }

    public async Task AddUserToGroupAsync(string currentUserId, Guid chatId, string targetUserName, CancellationToken cancellationToken = default)
    {
        var chat = await chatRepository.GetByIdAsync(chatId, cancellationToken) ?? throw new ChatNotFoundException("Chat not found");

        if (chat.Type != ChatType.Group)
        {
            throw new ChatOperationException("Cannot add members to a direct chat.");
        }

        if (!chat.Members.Any(m => m.UserId == currentUserId && m.LeftAtUtc == null))
        {
            throw new ChatUnauthorizedException("You are not a member of this chat.");
        }

        var targetUser = await userManager.FindByNameAsync(targetUserName)
                         ?? throw new ChatNotFoundException("Target user not found");

        if (chat.Members.Any(m => m.UserId == targetUser.Id && m.LeftAtUtc == null))
        {
            return;
        }

        chat.Members.Add(new ChatMember
        {
            ChatId = chat.Id,
            UserId = targetUser.Id,
            JoinedAtUtc = DateTime.UtcNow
        });

        await chatRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveUserFromGroupAsync(string currentUserId, Guid chatId, string targetUserName, CancellationToken cancellationToken = default)
    {
        var chat = await chatRepository.GetByIdAsync(chatId, cancellationToken) ?? throw new ChatNotFoundException("Chat not found");

        if (chat.Type != ChatType.Group)
        {
            throw new ChatOperationException("Cannot remove members from a direct chat.");
        }

        if (!chat.Members.Any(m => m.UserId == currentUserId && m.LeftAtUtc == null))
        {
            throw new ChatUnauthorizedException("You are not a member of this chat.");
        }

        var targetUser = await userManager.FindByNameAsync(targetUserName)
                         ?? throw new ChatNotFoundException("Target user not found");

        var membership = chat.Members.FirstOrDefault(m => m.UserId == targetUser.Id && m.LeftAtUtc == null);
        if (membership == null)
        {
            return;
        }

        membership.LeftAtUtc = DateTime.UtcNow;

        await chatRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveGroupChatAsync(string currentUserId, Guid chatId, CancellationToken cancellationToken = default)
    {
        var chat = await chatRepository.GetByIdAsync(chatId, cancellationToken) ?? throw new ChatNotFoundException("Chat not found");

        if (chat.Type != ChatType.Group)
        {
            throw new ChatOperationException("Cannot leave a direct chat.");
        }

        var membership = chat.Members.FirstOrDefault(m => m.UserId == currentUserId && m.LeftAtUtc == null)
                         ?? throw new ChatUnauthorizedException("You are not a member of this chat.");

        membership.LeftAtUtc = DateTime.UtcNow;

        await chatRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatDto>> GetUserChatsAsync(string currentUserId, CancellationToken cancellationToken = default)
    {
        var chats = await chatRepository.GetUserChatsAsync(currentUserId, cancellationToken);
        return chats.Select(MapToChatDto).ToList();
    }

    public Task<bool> IsUserInChatAsync(Guid chatId, string userId, CancellationToken cancellationToken = default)
    {
        return chatRepository.IsUserInChatAsync(chatId, userId, cancellationToken);
    }
    
    public async Task<IReadOnlyList<ChatUserDto>> GetChatUsersAsync(
        string currentUserId,
        Guid chatId,
        CancellationToken cancellationToken = default)
    {
        if (chatId == Guid.Empty)
        {
            throw new ChatOperationException("ChatId is required");
        }

        var currentUserExists = await userManager.Users.AnyAsync(u => u.Id == currentUserId, cancellationToken);
        if (!currentUserExists)
        {
            throw new ChatOperationException("Current user not found");
        }

        var chat = await chatRepository.GetByIdAsync(chatId, cancellationToken)
                   ?? throw new ChatNotFoundException("Chat not found");

        var isMember = chat.Members.Any(m => m.UserId == currentUserId && m.LeftAtUtc == null);
        if (!isMember)
        {
            throw new ChatUnauthorizedException("You are not a member of this chat.");
        }

        var members = chat.Members
            .Where(m => m.LeftAtUtc == null)
            .Select(m => new ChatUserDto
            {
                Id = m.UserId,
                UserName = m.User.UserName ?? string.Empty,
                IsOwner = m.IsOwner
            })
            .OrderBy(u => u.UserName)
            .ToList();

        return members;
    }

    private static ChatDto MapToChatDto(Chat chat)
    {
        var lastMessage = chat.Messages
            .OrderByDescending(m => m.SentAtUtc)
            .FirstOrDefault();
        return new ChatDto
        {
            Id = chat.Id,
            IsGroup = chat.Type == ChatType.Group,
            Name = chat.Name,
            LastMessagePreview = lastMessage?.Content,
            LastMessageAtUtc = lastMessage?.SentAtUtc,
            MemberCount = chat.Members.Count(m => m.LeftAtUtc == null)
        };
    }
}
