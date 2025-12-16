namespace Application.DTOs.Chat;

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string SenderUserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
}

