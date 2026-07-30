using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace VK.API.Auth;

/// <summary>
/// Generates signed JWT bearer tokens for tourists and admin users.
/// Configure Jwt:Key, Jwt:Issuer, Jwt:Audience in appsettings or environment variables.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a long-lived JWT for a tourist, embedding their touristId as the subject claim.
    /// </summary>
    string GenerateTouristToken(int touristId);

    /// <summary>
    /// Generates a JWT for an admin user with the Admin role claim.
    /// </summary>
    string GenerateAdminToken(int userId, string email);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateTouristToken(int touristId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, touristId.ToString()),
            new Claim(ClaimTypes.Role, "Tourist"),
            new Claim("tourist_id", touristId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        return CreateToken(claims);
    }

    public string GenerateAdminToken(int userId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        return CreateToken(claims);
    }

    private string CreateToken(IEnumerable<Claim> claims)
    {
        var rawKey = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(rawKey))
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it via the Jwt__Key environment variable or appsettings.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rawKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryDays = _config.GetValue<int>("Jwt:ExpiryDays", 365);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
