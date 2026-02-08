using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Application.DTOs;

public record RequestUpdateUserStatus(
    UserRoles Role    
);