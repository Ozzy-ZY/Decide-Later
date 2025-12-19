namespace Application.DTOs.Chat;

public record ChatUserDto
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public bool IsOwner { get; init; }
}
