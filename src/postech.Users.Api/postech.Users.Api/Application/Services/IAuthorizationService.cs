namespace postech.Users.Api.Application.Services;

public interface IAuthorizationService
{
    bool IsCurrentUserAdmin();
    Guid? GetCurrentUserId();
    string? GetCurrentUserRole();
}

