namespace Application.DTOs.Chat;

public class ChatDto
{
    public Guid Id { get; set; }
    public bool IsGroup { get; set; }
    public string? Name { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public int MemberCount { get; set; }
}

