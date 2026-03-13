using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using postech.Users.Api.Application.Services;
using postech.Users.Api.Domain.Entities;
using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Tests.Application.Services;

public class TokenServiceTests
{
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    
    private TokenService GetTokenService() => new TokenService(_configuration);

    private void SetupValidConfiguration()
    {
        _configuration["JwtSettings:SecretKey"].Returns("this-is-a-very-long-secret-key-for-jwt-testing-purposes-only");
        _configuration["JwtSettings:Issuer"].Returns("Postech.API");
        _configuration["JwtSettings:Audience"].Returns("Postech.Client");
    }

    // GenerateToken Tests

    [Fact]
    public void GenerateToken_WithValidUser_ShouldReturnValidToken()
    {
        // Arrange
        SetupValidConfiguration();
        var user = new User("test@email.com", "Test User", "hashed_pass", UserRoles.User);
        var tokenService = GetTokenService();

        // Act
        var token = tokenService.GenerateToken(user);

        // Assert
        token.Should().NotBeEmpty();
        token.Should().BeOfType<string>();
    }

    [Fact]
    public void GenerateToken_ShouldIncludeUserClaims()
    {
        // Arrange
        SetupValidConfiguration();
        var user = new User("test@email.com", "Test User", "hashed_pass", UserRoles.User);
        var tokenService = GetTokenService();

        // Act
        var token = tokenService.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        subClaim?.Value.Should().Be(user.Id.ToString());

        var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        emailClaim?.Value.Should().Be(user.Email);

        var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name);
        nameClaim?.Value.Should().Be(user.Name);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        roleClaim?.Value.Should().Be(user.Role.ToString());
    }

    [Fact]
    public void GenerateToken_ShouldHaveCorrectIssuer()
    {
        // Arrange
        SetupValidConfiguration();
        var user = new User("test@email.com", "Test User", "hashed_pass");
        var tokenService = GetTokenService();

        // Act
        var token = tokenService.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        jwtToken.Issuer.Should().Be("Postech.API");
    }

    [Fact]
    public void GenerateToken_ShouldHaveCorrectAudience()
    {
        // Arrange
        SetupValidConfiguration();
        var user = new User("test@email.com", "Test User", "hashed_pass");
        var tokenService = GetTokenService();

        // Act
        var token = tokenService.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        jwtToken.Audiences.Should().Contain("Postech.Client");
    }

    [Fact]
    public void GenerateToken_WithDifferentUsers_ShouldProduceDifferentTokens()
    {
        // Arrange
        SetupValidConfiguration();
        var user1 = new User("user1@email.com", "User One", "hashed_pass");
        var user2 = new User("user2@email.com", "User Two", "hashed_pass");
        var tokenService = GetTokenService();

        // Act
        var token1 = tokenService.GenerateToken(user1);
        var token2 = tokenService.GenerateToken(user2);

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateToken_WithoutSecretKey_ShouldThrowException()
    {
        // Arrange
        _configuration["JwtSettings:SecretKey"].Returns((string?)null);
        _configuration["JwtSettings:Issuer"].Returns("Postech.API");
        _configuration["JwtSettings:Audience"].Returns("Postech.Client");
        
        var user = new User("test@email.com", "Test User", "hashed_pass");
        var tokenService = GetTokenService();

        // Act & Assert
        var action = () => tokenService.GenerateToken(user);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Secret*");
    }

    [Fact]
    public void GenerateToken_WithoutIssuer_ShouldThrowException()
    {
        // Arrange
        _configuration["JwtSettings:SecretKey"].Returns("this-is-a-very-long-secret-key-for-jwt-testing-purposes-only");
        _configuration["JwtSettings:Issuer"].Returns((string?)null);
        _configuration["JwtSettings:Audience"].Returns("Postech.Client");
        
        var user = new User("test@email.com", "Test User", "hashed_pass");
        var tokenService = GetTokenService();

        // Act & Assert
        var action = () => tokenService.GenerateToken(user);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Issuer*");
    }

    [Fact]
    public void GenerateToken_WithoutAudience_ShouldThrowException()
    {
        // Arrange
        _configuration["JwtSettings:SecretKey"].Returns("this-is-a-very-long-secret-key-for-jwt-testing-purposes-only");
        _configuration["JwtSettings:Issuer"].Returns("Postech.API");
        _configuration["JwtSettings:Audience"].Returns((string?)null);
        
        var user = new User("test@email.com", "Test User", "hashed_pass");
        var tokenService = GetTokenService();

        // Act & Assert
        var action = () => tokenService.GenerateToken(user);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Audience*");
    }

    [Fact]
    public void GenerateToken_TokenShouldHaveExpiration()
    {
        // Arrange
        SetupValidConfiguration();
        var user = new User("test@email.com", "Test User", "hashed_pass");
        var tokenService = GetTokenService();

        // Act
        var token = tokenService.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
        jwtToken.ValidTo.Should().BeBefore(DateTime.UtcNow.AddHours(2));
    }

    [Fact]
    public void GenerateToken_ShouldIncludeJtiClaim()
    {
        // Arrange
        SetupValidConfiguration();
        var user = new User("test@email.com", "Test User", "hashed_pass");
        var tokenService = GetTokenService();

        // Act
        var token = tokenService.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        jtiClaim.Should().NotBeNull();
        jtiClaim?.Value.Should().NotBeEmpty();
    }
}
