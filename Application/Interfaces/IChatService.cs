using Application.DTOs.Chat;
namespace Application.Interfaces;

public interface IChatService
{
    Task<bool> IsUserInChatAsync(Guid chatId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatDto>> GetUserChatsAsync(string currentUserId, CancellationToken cancellationToken = default);

    Task LeaveGroupChatAsync(string currentUserId, Guid chatId, CancellationToken cancellationToken = default);

    Task RemoveUserFromGroupAsync(string currentUserId, Guid chatId, string targetUserName,
        CancellationToken cancellationToken = default);

    Task AddUserToGroupAsync(string currentUserId, Guid chatId, string targetUserName,
        CancellationToken cancellationToken = default);

    Task<ChatDto> CreateGroupChatAsync(string currentUserId, string name, IEnumerable<string> initialMemberUserNames,
        CancellationToken cancellationToken = default);

    Task<ChatDto> CreatePrivateChatAsync(string currentUserId, string targetUserName,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<ChatUserDto>> GetChatUsersAsync(
        string currentUserId,
        Guid chatId,
        CancellationToken cancellationToken = default);
}