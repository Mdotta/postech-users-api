namespace postech.Users.Api.Application.Services;

public interface ICognitoAuthService
{
    Task<string> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default);
}