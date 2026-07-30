using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.API.Auth;
using VK.Infrastructure.Data;

namespace VK.API.Controllers;

/// <summary>
/// Handles authentication for admin users.
/// Tourists authenticate via POST /api/Tourist/register which returns a JWT directly.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly IJwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        VKStreetFoodDbContext context,
        IJwtTokenService tokenService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Admin login. Returns a JWT bearer token on success.
    /// Use the returned token as: Authorization: Bearer {token}
    /// </summary>
    /// <remarks>
    /// Default seed credentials: admin@vkstreetfood.local / ChangeMe@2025!
    /// Change the password immediately after first login in production.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

        // Use constant-time path to avoid username enumeration timing attacks
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            PasswordHasher.Verify(request.Password, "dummy:dummyhash"); // constant-time burn
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (user.Role != "Admin")
            return StatusCode(403, new { message = "This endpoint is for admin users only." });

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin login succeeded for {Email}", user.Email);

        var token = _tokenService.GenerateAdminToken(user.Id, user.Email);

        return Ok(new
        {
            token,
            role = user.Role,
            userId = user.Id,
            email = user.Email
        });
    }

    /// <summary>
    /// Returns the authenticated user's identity info. Useful for validating that a token is still valid.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
            email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value,
            role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
