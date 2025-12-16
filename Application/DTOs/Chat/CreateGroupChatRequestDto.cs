using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Chat;

public class CreateGroupChatRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public List<string> InitialMemberUserNames { get; set; } = new();
}

