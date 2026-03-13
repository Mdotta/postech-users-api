using ErrorOr;
using postech.Users.Api.Application.DTOs;

namespace postech.Users.Api.Application.Services;

public interface IUserService
{
    Task<ErrorOr<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<string>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<UserResponse>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ErrorOr<Success>> UpdateRole(Guid id, RequestUpdateUserRole request, CancellationToken cancellationToken = default);
}