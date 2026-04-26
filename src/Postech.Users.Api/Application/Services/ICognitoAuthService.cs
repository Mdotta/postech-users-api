namespace postech.Users.Api.Application.Services;

using postech.Users.Api.Domain.Enums;

public interface ICognitoAuthService
{
    Task<string> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    // Registers the user in Cognito and returns the Cognito `sub` (user id)
    Task<string> RegisterAsync(string email, string password, string name, UserRoles role, CancellationToken cancellationToken = default);
    Task SetUserRoleAsync(string email, UserRoles role, CancellationToken cancellationToken = default);
}