using postech.Users.Api.Application.DTOs;
using Postech.Users.Api.Domain.Results;

namespace postech.Users.Api.Application.Services;

public interface IUserService
{
    Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<string>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserResponse>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
}