using Application.DTOs.Chat;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebProj.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatsController(IChatService chatService, IMessageService messageService) : ControllerBase
{
    [HttpPost("private")]
    public async Task<ActionResult<ChatDto>> CreatePrivateChat([FromBody] CreatePrivateChatRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var chat = await chatService.CreatePrivateChatAsync(userId, request.TargetUserName, cancellationToken);
        return Ok(chat);
    }

    [HttpPost("group")]
    public async Task<ActionResult<ChatDto>> CreateGroupChat([FromBody] CreateGroupChatRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var chat = await chatService.CreateGroupChatAsync(userId, request.Name, request.InitialMemberUserNames, cancellationToken);
        return Ok(chat);
    }

    [HttpPost("{chatId:guid}/members")]
    public async Task<IActionResult> AddUserToGroup(Guid chatId, [FromBody] AddUserToGroupRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await chatService.AddUserToGroupAsync(userId, chatId, request.UserName, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{chatId:guid}/members/{userName}")]
    public async Task<IActionResult> RemoveUserFromGroup(Guid chatId, string userName, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await chatService.RemoveUserFromGroupAsync(userId, chatId, userName, cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/leave")]
    public async Task<IActionResult> LeaveGroup(Guid chatId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await chatService.LeaveGroupChatAsync(userId, chatId, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatDto>>> GetUserChats(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var chats = await chatService.GetUserChatsAsync(userId, cancellationToken);
        return Ok(chats);
    }

    [HttpGet("{chatId:guid}/messages")]
    public async Task<ActionResult<PagedResultDto<MessageDto>>> GetMessages(Guid chatId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var messages = await messageService.GetMessagesAsync(userId, chatId, pageNumber, pageSize, cancellationToken);
        return Ok(messages);
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new InvalidOperationException("User is not authenticated.");
    }
}

public class AddUserToGroupRequestDto
{
    public string UserName { get; set; } = string.Empty;
}

