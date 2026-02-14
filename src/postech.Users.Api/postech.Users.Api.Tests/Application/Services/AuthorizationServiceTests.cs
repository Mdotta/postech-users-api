using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using postech.Users.Api.Application.Services;
using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Tests.Application.Services;

public class AuthorizationServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    
    private AuthorizationService GetAuthorizationService() => new AuthorizationService(_httpContextAccessor);

    // IsCurrentUserAdmin Tests

    [Fact]
    public void IsCurrentUserAdmin_WhenRoleIsAdministrator_ShouldReturnTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.Role, nameof(UserRoles.Administrator)) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.IsCurrentUserAdmin();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCurrentUserAdmin_WhenRoleIsUser_ShouldReturnFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.Role, nameof(UserRoles.User)) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.IsCurrentUserAdmin();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentUserAdmin_WhenRoleIsNull_ShouldReturnFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new List<Claim>());
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.IsCurrentUserAdmin();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentUserAdmin_WhenHttpContextIsNull_ShouldReturnFalse()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.IsCurrentUserAdmin();

        // Assert
        result.Should().BeFalse();
    }

    // GetCurrentUserId Tests

    [Fact]
    public void GetCurrentUserId_WithValidUserId_ShouldReturnId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.GetCurrentUserId();

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WithInvalidUserId_ShouldReturnNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "invalid-guid") };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WithoutUserId_ShouldReturnNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new List<Claim>());
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WhenHttpContextIsNull_ShouldReturnNull()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    // GetCurrentUserRole Tests

    [Fact]
    public void GetCurrentUserRole_WithValidRole_ShouldReturnRole()
    {
        // Arrange
        var expectedRole = nameof(UserRoles.Administrator);
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.Role, expectedRole) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.GetCurrentUserRole();

        // Assert
        result.Should().Be(expectedRole);
    }

    [Fact]
    public void GetCurrentUserRole_WithoutRole_ShouldReturnNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new List<Claim>());
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        _httpContextAccessor.HttpContext.Returns(httpContext);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.GetCurrentUserRole();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserRole_WhenHttpContextIsNull_ShouldReturnNull()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        
        var authService = GetAuthorizationService();

        // Act
        var result = authService.GetCurrentUserRole();

        // Assert
        result.Should().BeNull();
    }
}
