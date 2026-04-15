using Amazon.CognitoIdentityProvider.Model;
using ErrorOr;
using postech.Users.Api.Application.DTOs;
using postech.Users.Api.Application.Validations;
using Postech.Shared.Contracts.Events;
using postech.Users.Api.Domain.Entities;
using postech.Users.Api.Domain.Enums;
using postech.Users.Api.Domain.Errors;
using postech.Users.Api.Infrastructure.Messaging;
using postech.Users.Api.Infrastructure.Repositories;

namespace postech.Users.Api.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ICognitoAuthService _cognitoAuthService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<UserService> _logger;
    private readonly IAuthorizationService _authorizationService;

    public UserService(
        IUserRepository userRepository,
        ICognitoAuthService cognitoAuthService,
        IEventPublisher eventPublisher,
        ILogger<UserService> logger,
        IAuthorizationService authorizationService)
    {
        _userRepository = userRepository;
        _cognitoAuthService = cognitoAuthService;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _authorizationService = authorizationService;
    }

    public async Task<ErrorOr<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering user with email {Email}", request.Email);

        var validationResult = RegisterUserRequestValidator.Validate(request);
        if (validationResult.IsError)
            return validationResult.Errors;

        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
            return Errors.User.EmailAlreadyExists;
        }

        var role = request.Role ?? UserRoles.User;

        if (role == UserRoles.Administrator)
        {
            if (!_authorizationService.IsCurrentUserAdmin())
            {
                _logger.LogWarning("Non-admin user attempted to create admin account for email {Email}", request.Email);
                return Errors.User.ForbiddenAdminCreation;
            }

            _logger.LogInformation("Admin user creating another admin account");
        }

        // Register in Cognito first — if this fails, we don't save to DB
        try
        {
            await _cognitoAuthService.RegisterAsync(request.Email, request.Password, request.Name, cancellationToken);
        }
        catch (UsernameExistsException)
        {
            _logger.LogWarning("Cognito registration failed: Email {Email} already exists in Cognito", request.Email);
            return Errors.User.EmailAlreadyExists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cognito registration failed for email {Email}", request.Email);
            return Error.Failure("Cognito.RegistrationFailed", "Failed to register user in authentication provider.");
        }

        // Save to local DB
        var passwordHash = User.HashPassword(request.Password);
        var user = new User(request.Email, request.Name, passwordHash, role);
        await _userRepository.AddAsync(user, cancellationToken);

        var userCreatedEvent = new UserCreatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            CreatedAt = user.CreatedAt
        };

        await _eventPublisher.PublishAsync(userCreatedEvent, cancellationToken);

        _logger.LogInformation("User {UserId} registered successfully", user.Id);

        return MapToResponse(user);
    }

    public async Task<ErrorOr<string>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Logging in user with email {Email}", request.Email);

        // Verify user exists in our DB
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Login failed: User {Email} not found in DB", request.Email);
            return Errors.User.InvalidCredentials;
        }

        // Authenticate via Cognito — returns Cognito IdToken
        try
        {
            var token = await _cognitoAuthService.LoginAsync(request.Email, request.Password, cancellationToken);
            _logger.LogInformation("User {UserId} logged in successfully", user.Id);
            return token;
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("Login failed: Invalid credentials for email {Email}", request.Email);
            return Errors.User.InvalidCredentials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cognito login failed for email {Email}", request.Email);
            return Error.Failure("Cognito.LoginFailed", "Authentication provider error.");
        }
    }

    public async Task<ErrorOr<UserResponse>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user == null)
            return Errors.User.NotFound;

        return MapToResponse(user);
    }

    public async Task<ErrorOr<Success>> UpdateRole(Guid id, RequestUpdateUserRole request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating user {UserId} role to {Role}", id, request.Role);

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Update role failed: User {UserId} not found", id);
            return Errors.User.NotFound;
        }

        user.UpdateRole(request.Role);
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User {UserId} role updated successfully to {Role}", id, request.Role);

        return Result.Success;
    }

    private static UserResponse MapToResponse(User user)
        => new UserResponse(user.Id, user.Email, user.Name, user.Role.ToString(), user.CreatedAt);
}