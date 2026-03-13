using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using postech.Users.Api.Domain.Entities;

namespace postech.Users.Api.Application.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }


    public string GenerateToken(User user)
    {
        var jwtSecret = _configuration["JwtSettings:SecretKey"] 
                        ?? throw new InvalidOperationException("JWT Secret is not configured");

        var jwtIssuer = _configuration["JwtSettings:Issuer"]
                        ?? throw new InvalidOperationException("JWT Issuer is not configured");

        var jwtAudience = _configuration["JwtSettings:Audience"]
                        ?? throw new InvalidOperationException("JWT Audience is not configured");
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}