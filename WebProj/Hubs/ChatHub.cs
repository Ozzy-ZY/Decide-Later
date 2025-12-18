using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Application.Exceptions;

namespace WebProj.Hubs;

[Authorize]
public class ChatHub(IMessageService messageService, IChatService chatService) : Hub
{
    public async Task JoinChat(Guid chatId)
    {
        var userId = GetCurrentUserId();
        var isMember = await chatService.IsUserInChatAsync(chatId, userId);
        if (!isMember)
        {
            throw new ChatUnauthorizedException("You are not a member of this chat.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(chatId));
    }

    public async Task LeaveChat(Guid chatId)
    {
        var userId = GetCurrentUserId();
        var isMember = await chatService.IsUserInChatAsync(chatId, userId);
        if (!isMember)
        {
            throw new ChatUnauthorizedException("You are not a member of this chat.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(chatId));
    }

    public async Task SendMessage(Guid chatId, string content)
    {
        var userId = GetCurrentUserId();

        var message = await messageService.SendMessageAsync(userId, chatId, content);

        await Clients.Group(GetGroupName(chatId)).SendAsync("ReceiveMessage", message);
    }

    private string GetCurrentUserId()
    {
        return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new ChatUnauthorizedException("User is not authenticated.");
    }

    private static string GetGroupName(Guid chatId) => $"chat-{chatId}";
}

