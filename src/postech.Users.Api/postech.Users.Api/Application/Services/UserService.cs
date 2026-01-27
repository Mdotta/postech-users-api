using postech.Users.Api.Application.DTOs;
using postech.Users.Api.Application.Events;
using postech.Users.Api.Domain.Entities;
using postech.Users.Api.Domain.Enums;
using Postech.Users.Api.Domain.Results;
using postech.Users.Api.Infrastructure.Messaging;
using postech.Users.Api.Infrastructure.Repositories;

namespace postech.Users.Api.Application.Services;

public class UserService: IUserService
{
    
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository,
        ITokenService tokenService,
        IEventPublisher eventPublisher,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering user with email {Email}", request.Email);

        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
            return Result.Failure<UserResponse>("Email already registered"); //TODO: validate message for security reasons
        }
        
        var role = UserRoles.User;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse(request.Role!, true, out role))
            {
                _logger.LogWarning("Invalid role '{Role}' provided for email {Email}, defaulting to User", request.Role, request.Email);
                role = UserRoles.User;
            }
        }
        
        if (role == UserRoles.Administrator)
        {
            _logger.LogWarning("Attempting to create Admin user - validation required");
        }
        
        var passwordHash = User.HashPassword(request.Password);
        var user = new User(request.Email,request.Name, passwordHash, role);
        
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

        var response = MapToResponse(user);
        
        return Result.Success(response);
    }

    public async Task<Result<string>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Logining user with email {Email}", request.Email);
        
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null || !user.VerifyPassword(request.Password))
        {
            _logger.LogWarning("Login failed: Email {Email} does not exist", request.Email);
            return Result.Failure<string>("Email does not exist");
        } 
        
        var token = _tokenService.GenerateToken(user);
    
        _logger.LogInformation("User {UserId} logged in successfully", user.Id);
        
        return Result.Success(token);
    }

    public async Task<Result<UserResponse>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user == null)
        {
            return Result.Failure<UserResponse>("User not found");
        }
        
        var response = MapToResponse(user);
        
        return Result.Success(response);
    }
    
    private static UserResponse MapToResponse(User user)
    {
        var response = new UserResponse(user.Id, user.Email, user.Name, user.Role.ToString(), user.CreatedAt);
        return response;
    }
}