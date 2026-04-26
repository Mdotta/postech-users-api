using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Postech.Shared.Contracts.Events;
using postech.Users.Api.Application.DTOs;
using postech.Users.Api.Application.Services;
using postech.Users.Api.Domain.Entities;
using postech.Users.Api.Domain.Enums;
using postech.Users.Api.Domain.Errors;
using postech.Users.Api.Infrastructure.Messaging;
using postech.Users.Api.Infrastructure.Repositories;

namespace postech.Users.Api.Tests.Application.Services;

public class UserServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICognitoAuthService _cognitoAuthService = Substitute.For<ICognitoAuthService>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly ILogger<UserService> _logger = Substitute.For<ILogger<UserService>>();
    private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();

    private UserService GetUserService() => new UserService(
        _userRepository,
        _cognitoAuthService,
        _eventPublisher,
        _logger,
        _authorizationService);

    // RegisterAsync Tests

    [Fact]
    public async Task RegisterAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new RegisterUserRequest("test@email.com", "Test User", "ValidP@ssw0rd123", UserRoles.User);
        _userRepository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>()).Returns(false);
        _authorizationService.IsCurrentUserAdmin().Returns(false);
        _cognitoAuthService.RegisterAsync(request.Email, request.Password, request.Name, UserRoles.User, Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid().ToString());

        var userService = GetUserService();

        // Act
        var result = await userService.RegisterAsync(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be(request.Email);
        result.Value.Name.Should().Be(request.Name);
        await _cognitoAuthService.Received(1).RegisterAsync(request.Email, request.Password, request.Name, UserRoles.User, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(Arg.Any<UserCreatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ShouldReturnConflict()
    {
        // Arrange
        var request = new RegisterUserRequest("existing@email.com", "Test User", "ValidP@ssw0rd123", UserRoles.User);
        _userRepository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>()).Returns(true);

        var userService = GetUserService();

        // Act
        var result = await userService.RegisterAsync(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.User.EmailAlreadyExists.Code);
        await _cognitoAuthService.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserRoles>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithInvalidRequest_ShouldReturnValidationErrors()
    {
        // Arrange
        var request = new RegisterUserRequest("invalid@email", "", "weak", UserRoles.User);

        var userService = GetUserService();

        // Act
        var result = await userService.RegisterAsync(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCountGreaterThan(0);
        await _cognitoAuthService.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserRoles>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithAdminRole_ByNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        var request = new RegisterUserRequest("test@email.com", "Test User", "ValidP@ssw0rd123", UserRoles.Administrator);
        _userRepository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>()).Returns(false);
        _authorizationService.IsCurrentUserAdmin().Returns(false);

        var userService = GetUserService();

        // Act
        var result = await userService.RegisterAsync(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.User.ForbiddenAdminCreation.Code);
        await _cognitoAuthService.DidNotReceive().RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserRoles>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithAdminRole_ByAdmin_ShouldSucceed()
    {
        // Arrange
        var request = new RegisterUserRequest("admin@email.com", "Admin User", "ValidP@ssw0rd123", UserRoles.Administrator);
        _userRepository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>()).Returns(false);
        _authorizationService.IsCurrentUserAdmin().Returns(true);
        _cognitoAuthService.RegisterAsync(request.Email, request.Password, request.Name, UserRoles.Administrator, Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid().ToString());

        var userService = GetUserService();

        // Act
        var result = await userService.RegisterAsync(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Role.Should().Be(UserRoles.Administrator.ToString());
        await _cognitoAuthService.Received(1).RegisterAsync(request.Email, request.Password, request.Name, UserRoles.Administrator, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ShouldPublishUserCreatedEvent()
    {
        // Arrange
        var request = new RegisterUserRequest("test@email.com", "Test User", "ValidP@ssw0rd123", UserRoles.User);
        _userRepository.EmailExistsAsync(request.Email, Arg.Any<CancellationToken>()).Returns(false);
        _authorizationService.IsCurrentUserAdmin().Returns(false);
        _cognitoAuthService.RegisterAsync(request.Email, request.Password, request.Name, UserRoles.User, Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid().ToString());

        var userService = GetUserService();

        // Act
        await userService.RegisterAsync(request);

        // Assert
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<UserCreatedEvent>(e => e.Email == request.Email && e.Name == request.Name),
            Arg.Any<CancellationToken>());
    }

    // LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var password = "ValidP@ssw0rd123";
        var hashedPassword = User.HashPassword(password);
        var user = new User("test@email.com", "Test User", hashedPassword);
        var expectedToken = "cognito_id_token_123";

        _userRepository.GetByEmailAsync("test@email.com", Arg.Any<CancellationToken>()).Returns(user);
        _cognitoAuthService.LoginAsync("test@email.com", password, Arg.Any<CancellationToken>()).Returns(expectedToken);

        var request = new LoginRequest("test@email.com", password);
        var userService = GetUserService();

        // Act
        var result = await userService.LoginAsync(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(expectedToken);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmail_ShouldReturnError()
    {
        // Arrange
        _userRepository.GetByEmailAsync("nonexistent@email.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        var request = new LoginRequest("nonexistent@email.com", "password");
        var userService = GetUserService();

        // Act
        var result = await userService.LoginAsync(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.User.InvalidCredentials.Code);
        await _cognitoAuthService.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ShouldReturnError()
    {
        // Arrange
        var password = "ValidP@ssw0rd123";
        var hashedPassword = User.HashPassword(password);
        var user = new User("test@email.com", "Test User", hashedPassword);

        _userRepository.GetByEmailAsync("test@email.com", Arg.Any<CancellationToken>()).Returns(user);
        _cognitoAuthService
            .LoginAsync("test@email.com", "WrongPassword123", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new Amazon.CognitoIdentityProvider.Model.NotAuthorizedException("Invalid credentials")));

        var request = new LoginRequest("test@email.com", "WrongPassword123");
        var userService = GetUserService();

        // Act
        var result = await userService.LoginAsync(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.User.InvalidCredentials.Code);
    }

    // GetUserByIdAsync Tests

    [Fact]
    public async Task GetUserByIdAsync_WithExistingId_ShouldReturnUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("test@email.com", "Test User", "hashed_pass");
        user.Id = userId;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var userService = GetUserService();

        // Act
        var result = await userService.GetUserByIdAsync(userId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be(user.Email);
        result.Value.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithNonExistingId_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var userService = GetUserService();

        // Act
        var result = await userService.GetUserByIdAsync(userId);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.User.NotFound.Code);
    }

    // UpdateRole Tests

    [Fact]
    public async Task UpdateRole_WithExistingUser_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User("test@email.com", "Test User", "hashed_pass");
        user.Id = userId;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var request = new RequestUpdateUserRole(UserRoles.Administrator);
        var userService = GetUserService();

        // Act
        var result = await userService.UpdateRole(userId, request);

        // Assert
        result.IsError.Should().BeFalse();
        await _userRepository.Received(1).UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _cognitoAuthService.Received(1).SetUserRoleAsync(user.Email, UserRoles.Administrator, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRole_WithNonExistingUser_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var request = new RequestUpdateUserRole(UserRoles.Administrator);
        var userService = GetUserService();

        // Act
        var result = await userService.UpdateRole(userId, request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.User.NotFound.Code);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
