using System.Security.Claims;
using AppClaimTypes = postech.Users.Api.Application.Constants.ClaimTypes;
using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Application.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthorizationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsCurrentUserAdmin()
    {
        var role = GetCurrentUserRole();
        return role?.Equals(nameof(UserRoles.Administrator), StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public Guid? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userIdClaim = user?.FindFirst(AppClaimTypes.AppUserId)?.Value
                          ?? user?.FindFirst(AppClaimTypes.AlternateAppUserId)?.Value
                          ?? user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public string? GetCurrentUserRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(AppClaimTypes.CognitoGroups)?.Value
               ?? user?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
               ?? user?.FindFirst(AppClaimTypes.Role)?.Value;
    }
}

