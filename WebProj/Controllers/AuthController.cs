using System.Security.Claims;
using Application.DTOs;
using Application.DTOs.Auth;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebProj.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IAuthService authService,
    IJwtService jwtService,
    IWebHostEnvironment environment)
    : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerDto)
    {
        var (response, refreshToken) = await authService.RegisterAsync(registerDto);

        if (!response.Success)
        {
            return BadRequest(response);
        }

        if (string.IsNullOrEmpty(refreshToken))
            return Ok(response);
        SetRefreshTokenCookie(refreshToken);
        response.RefreshToken = HttpContext.Request.Headers["X-Client-Type"].ToString() == "server" ? refreshToken : null;

        return Ok(response);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto loginDto)
    {

        var (response, refreshToken) = await authService.LoginAsync(loginDto);

        if (!response.Success)
        {
            return Unauthorized(response);
        }

        if (string.IsNullOrEmpty(refreshToken))
            return Ok(response);
        SetRefreshTokenCookie(refreshToken);
        response.RefreshToken = HttpContext.Request.Headers["X-Client-Type"].ToString() == "server" ? refreshToken : null;

        return Ok(response);
    }

    /// <summary>
    /// Refresh access token using refresh token from cookie
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        var clientType = HttpContext.Request.Headers["X-Client-Type"].ToString();
        if (clientType == "server")
        {
            refreshToken = Request.Headers["Refresh-Token"].ToString();
        }
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new AuthResponseDto
            {
                Success = false,
                Errors = new[] { "Refresh token not found" }
            });
        }

        var (response, newRefreshToken) = await authService.RefreshTokenAsync(refreshToken);

        if (!response.Success)
        {
            DeleteRefreshTokenCookie();
            return Unauthorized(response);
        }

        if (string.IsNullOrEmpty(newRefreshToken))
            return Ok(response);
        SetRefreshTokenCookie(newRefreshToken);
        response.RefreshToken = HttpContext.Request.Headers["X-Client-Type"].ToString() == "server" ? newRefreshToken : null;

        return Ok(response);
    }

    /// <summary>
    /// Change password (requires authentication and current password)
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ChangePasswordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ChangePasswordResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto changePasswordDto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await authService.ChangePasswordAsync(userId, changePasswordDto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        // Clear refresh token cookie after password change
        DeleteRefreshTokenCookie();

        return Ok(result);
    }

    /// <summary>
    /// Logout - revokes the current refresh token
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await authService.RevokeTokenAsync(refreshToken, "User logged out");
            DeleteRefreshTokenCookie();
        }
        var clientType = HttpContext.Request.Headers["X-Client-Type"].ToString();
        if (clientType != "server")
        {
            return Ok(new { message = "Logged out successfully" });
        }
        refreshToken = Request.Headers["Refresh-Token"].ToString();
        await authService.RevokeTokenAsync(refreshToken, "User logged out");

        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await authService.GetUserProfileAsync(userId);

        if (profile == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(profile);
    }

    /// <summary>
    /// Revoke all refresh tokens for the current user (logout from all devices)
    /// </summary>
    [HttpPost("revoke-all-tokens")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllTokens()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await authService.RevokeAllUserTokensAsync(userId, "User revoked all tokens");
        DeleteRefreshTokenCookie();

        return Ok(new { message = "All tokens revoked successfully" });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(), 
            SameSite = SameSiteMode.Lax, // Lax allows the cookie to be sent on navigation
            Expires = DateTime.UtcNow.AddDays(jwtService.GetRefreshTokenExpirationDays()),
            Path = "/", 
            IsEssential = true
        };

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, cookieOptions);
    }

    private void DeleteRefreshTokenCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(-1),
            Path = "/" 
        };

        Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
    }
}
