namespace Application.DTOs.Chat;

using System.ComponentModel.DataAnnotations;

public class CreatePrivateChatRequestDto
{
    [Required]
    [MaxLength(256)]
    public string TargetUserName { get; set; } = string.Empty;
}
