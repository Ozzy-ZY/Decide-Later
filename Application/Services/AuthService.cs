using Application.DTOs.Auth;
using Application.Interfaces;
using Domain.Models;
using Infrastructure.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtService jwtService,
    IRefreshTokenRepository refreshTokenRepository,
    IConfiguration configuration,
    ILogger<AuthService> logger)
    : IAuthService
{
    public async Task<(AuthResponseDto Response, string? RefreshToken)> RegisterAsync(RegisterRequestDto registerDto)
    {
        logger.LogInformation("Registration attempt for email: {Email}, username: {UserName}", 
            registerDto.Email, registerDto.UserName);

        var existingUserByEmail = await userManager.FindByEmailAsync(registerDto.Email);
        if (existingUserByEmail != null)
        {
            logger.LogWarning("Registration failed - email already exists: {Email}", registerDto.Email);
            return (new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "Email is already registered" }
            }, null);
        }

        var existingUserByUsername = await userManager.FindByNameAsync(registerDto.UserName);
        if (existingUserByUsername != null)
        {
            logger.LogWarning("Registration failed - username already taken: {UserName}", registerDto.UserName);
            return (new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "Username is already taken" }
            }, null);
        }

        var user = new ApplicationUser
        {
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            UserName = registerDto.UserName,
            Email = registerDto.Email,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
        {
            logger.LogWarning("Registration failed for {Email} - Identity errors: {Errors}", 
                registerDto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return (new AuthResponseDto
            {
                Success = false,
                Errors = result.Errors.Select(e => e.Description)
            }, null);
        }

        logger.LogInformation("User registered successfully: {UserId}, {Email}", user.Id, user.Email);

        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = jwtService.GenerateRefreshToken(user.Id);
        await refreshTokenRepository.AddAsync(refreshToken);
        logger.LogDebug("Refresh token created for user: {UserId}", user.Id);

        var expirationMinutes = int.Parse(configuration.GetSection("JwtSettings")["ExpirationInMinutes"] ?? "60");

        return (new AuthResponseDto
        {
            Success = true,
            Token = accessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
            User = MapToUserProfileDto(user)
        }, refreshToken.Token);
    }

    public async Task<(AuthResponseDto Response, string? RefreshToken)> LoginAsync(LoginRequestDto loginDto)
    {
        logger.LogInformation("Login attempt for email: {Email}", loginDto.Email);

        var user = await userManager.FindByEmailAsync(loginDto.Email);
        
        if (user == null)
        {
            logger.LogWarning("Login failed - user not found: {Email}", loginDto.Email);
            return (new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "Invalid email or password" }
            }, null);
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Login failed - account locked out: {UserId}, {Email}", user.Id, loginDto.Email);
            return (new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "Account is locked. Please try again later." }
            }, null);
        }

        if (!result.Succeeded)
        {
            logger.LogWarning("Login failed - invalid password for user: {UserId}, {Email}", user.Id, loginDto.Email);
            return (new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "Invalid email or password" }
            }, null);
        }
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        logger.LogInformation("User logged in successfully: {UserId}, {Email}", user.Id, user.Email);
        
        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = jwtService.GenerateRefreshToken(user.Id);
        
        await refreshTokenRepository.AddAsync(refreshToken);
        logger.LogDebug("Refresh token created for user: {UserId}", user.Id);

        var expirationMinutes = int.Parse(configuration.GetSection("JwtSettings")["ExpirationInMinutes"] ?? "60");

        return (new AuthResponseDto
        {
            Success = true,
            Token = accessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
            User = MapToUserProfileDto(user)
        }, refreshToken.Token);
    }

    public async Task<(AuthResponseDto Response, string? RefreshToken)> RefreshTokenAsync(string refreshToken)
    {
        logger.LogDebug("Token refresh attempt");

        var storedToken = await refreshTokenRepository.GetByTokenAsync(refreshToken);

        if (storedToken == null)
        {
            logger.LogWarning("Token refresh failed - invalid token provided");
            return (new AuthResponseDto
            {
                Success = false,
                Errors = ["Invalid refresh token"]
            }, null);
        }

        if (!storedToken.IsActive)
        {
            // If token is revoked or expired, revoke all tokens for security kick their ahh
            if (storedToken.IsRevoked)
            {
                logger.LogWarning("Security alert - attempted reuse of revoked token for user: {UserId}. Revoking all tokens.", 
                    storedToken.UserId);
                await refreshTokenRepository.RevokeAllUserTokensAsync(
                    storedToken.UserId, 
                    "Attempted reuse of revoked token");
            }
            else
            {
                logger.LogWarning("Token refresh failed - token expired for user: {UserId}", storedToken.UserId);
            }

            return (new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "Refresh token is no longer valid" }
            }, null);
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            logger.LogWarning("Token refresh failed - user not found: {UserId}", storedToken.UserId);
            return (new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "User not found" }
            }, null);
        }

        storedToken.RevokedAt = DateTime.UtcNow; // rotate bratha
        storedToken.ReasonRevoked = "Replaced by new token";

        var newAccessToken = jwtService.GenerateAccessToken(user);
        var newRefreshToken = jwtService.GenerateRefreshToken(user.Id);
        
        storedToken.ReplacedByToken = newRefreshToken.Token;
        await refreshTokenRepository.UpdateAsync(storedToken);
        await refreshTokenRepository.AddAsync(newRefreshToken);

        logger.LogInformation("Token refreshed successfully for user: {UserId}", user.Id);

        var expirationMinutes = int.Parse(configuration.GetSection("JwtSettings")["ExpirationInMinutes"] ?? "60");

        return (new AuthResponseDto
        {
            Success = true,
            Token = newAccessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
            User = MapToUserProfileDto(user)
        }, newRefreshToken.Token);
    }

    public async Task<ChangePasswordResponseDto> ChangePasswordAsync(string userId, ChangePasswordRequestDto changePasswordDto)
    {
        logger.LogInformation("Password change attempt for user: {UserId}", userId);

        var user = await userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            logger.LogWarning("Password change failed - user not found: {UserId}", userId);
            return new ChangePasswordResponseDto
            {
                Success = false,
                Errors = new[] { "User not found" }
            };
        }

        var isCurrentPasswordValid = await userManager.CheckPasswordAsync(user, changePasswordDto.CurrentPassword);
        if (!isCurrentPasswordValid)
        {
            logger.LogWarning("Password change failed - incorrect current password for user: {UserId}", userId);
            return new ChangePasswordResponseDto
            {
                Success = false,
                Errors = new[] { "Current password is incorrect" }
            };
        }
        var result = await userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);

        if (!result.Succeeded)
        {
            logger.LogWarning("Password change failed for user {UserId} - Identity errors: {Errors}", 
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return new ChangePasswordResponseDto
            {
                Success = false,
                Errors = result.Errors.Select(e => e.Description)
            };
        }

        await refreshTokenRepository.RevokeAllUserTokensAsync(userId, "Password changed");
        
        logger.LogInformation("Password changed successfully for user: {UserId}. All refresh tokens revoked.", userId);

        return new ChangePasswordResponseDto
        {
            Success = true,
            Message = "Password changed successfully. Please login again."
        };
    }

    public async Task<UserProfileResponseDto?> GetUserProfileAsync(string userId)
    {
        logger.LogDebug("Fetching profile for user: {UserId}", userId);

        var user = await userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            logger.LogWarning("Profile fetch failed - user not found: {UserId}", userId);
            return null;
        }

        return MapToUserProfileDto(user);
    }

    public async Task RevokeTokenAsync(string refreshToken, string reason = "Revoked by user")
    {
        var storedToken = await refreshTokenRepository.GetByTokenAsync(refreshToken);
        
        if (storedToken != null && storedToken.IsActive)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.ReasonRevoked = reason;
            await refreshTokenRepository.UpdateAsync(storedToken);
            logger.LogInformation("Refresh token revoked for user: {UserId}, reason: {Reason}", 
                storedToken.UserId, reason);
        }
    }

    public async Task RevokeAllUserTokensAsync(string userId, string reason = "Revoked all tokens")
    {
        await refreshTokenRepository.RevokeAllUserTokensAsync(userId, reason);
        logger.LogInformation("All refresh tokens revoked for user: {UserId}, reason: {Reason}", userId, reason);
    }
    private static UserProfileResponseDto MapToUserProfileDto(ApplicationUser user)
    {
        return new UserProfileResponseDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}
