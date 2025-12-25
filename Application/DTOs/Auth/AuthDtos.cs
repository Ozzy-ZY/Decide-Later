namespace Application.DTOs.Auth;

public record RegisterRequestDto
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string ConfirmPassword { get; init; }
}

public record LoginRequestDto
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public record ChangePasswordRequestDto
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
    public required string ConfirmNewPassword { get; init; }
}

public record UserProfileResponseDto
{
    public string Id { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}

public record AuthResponseDto
{
    public bool Success { get; init; }
    public string? Token { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public UserProfileResponseDto? User { get; init; }
    public IEnumerable<string>? Errors { get; init; }
    
    public string? RefreshToken { get; set; }
}

public record ChangePasswordResponseDto
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IEnumerable<string>? Errors { get; init; }
}
