using Application.DTOs;
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
    /// <summary>
    /// Creates a new private chat with another user.
    /// </summary>
    [HttpPost("private")]
    [ProducesResponseType(typeof(ChatDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChatDto>> CreatePrivateChat([FromBody] CreatePrivateChatRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var chat = await chatService.CreatePrivateChatAsync(userId, request.TargetUserName, cancellationToken);
        return Ok(chat);
    }

    /// <summary>
    /// Creates a new group chat with multiple users.
    /// </summary>
    [HttpPost("group")]
    [ProducesResponseType(typeof(ChatDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChatDto>> CreateGroupChat([FromBody] CreateGroupChatRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var chat = await chatService.CreateGroupChatAsync(userId, request.Name, request.InitialMemberUserNames, cancellationToken);
        return Ok(chat);
    }

    /// <summary>
    /// Adds a user to an existing group chat.
    /// </summary>
    [HttpPost("{chatId:guid}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddUserToGroup(Guid chatId, [FromBody] AddUserToGroupRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await chatService.AddUserToGroupAsync(userId, chatId, request.UserName, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a user from a group chat.
    /// </summary>
    [HttpDelete("{chatId:guid}/members/{userName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUserFromGroup(Guid chatId, string userName, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await chatService.RemoveUserFromGroupAsync(userId, chatId, userName, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Leaves a group chat.
    /// </summary>
    [HttpPost("{chatId:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveGroup(Guid chatId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await chatService.LeaveGroupChatAsync(userId, chatId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves all chats for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ChatDto>>> GetUserChats(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var chats = await chatService.GetUserChatsAsync(userId, cancellationToken);
        return Ok(chats);
    }

    /// <summary>
    /// Retrieves messages for a specific chat with pagination.
    /// </summary>
    [HttpGet("{chatId:guid}/messages")]
    [ProducesResponseType(typeof(PagedResultDto<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResultDto<MessageDto>>> GetMessages(Guid chatId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var messages = await messageService.GetMessagesAsync(userId, chatId, pageNumber, pageSize, cancellationToken);
        return Ok(messages);
    }

    /// <summary>
    /// Retrieves active members of a chat.
    /// The caller must be an active member of that chat.
    /// </summary>
    [HttpGet("{chatId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ChatUserDto>>> GetChatMembers(Guid chatId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var users = await chatService.GetChatUsersAsync(userId, chatId, cancellationToken);
        return Ok(users);
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
