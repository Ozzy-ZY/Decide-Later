using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel;
using System.Security.Claims;

namespace WebProj.Hubs;

[Authorize]
public class ChatHub(IMessageService messageService, IChatService chatService) : Hub
{
    [Description("Joins a user to a specific chat group to receive real-time updates.")]
    public async Task JoinChat(Guid chatId)
    {
        var userId = GetCurrentUserId();
        var isMember = await chatService.IsUserInChatAsync(chatId, userId);
        if (!isMember)
        {
            throw new HubException("You are not a member of this chat.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(chatId));
    }

    [Description("Removes a user from a specific chat group.")]
    public async Task LeaveChat(Guid chatId)
    {
        var userId = GetCurrentUserId();
        var isMember = await chatService.IsUserInChatAsync(chatId, userId);
        if (!isMember)
        {
            throw new HubException("You are not a member of this chat.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(chatId));
    }

    [Description("Sends a message to a specific chat group and notifies all members.")]
    public async Task SendMessage(Guid chatId, string content)
    {
        var userId = GetCurrentUserId();

        var message = await messageService.SendMessageAsync(userId, chatId, content);

        await Clients.Group(GetGroupName(chatId)).SendAsync("ReceiveMessage", message);
    }

    private string GetCurrentUserId()
    {
        return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new HubException("User is not authenticated.");
    }

    private static string GetGroupName(Guid chatId) => $"chat-{chatId}";
}
