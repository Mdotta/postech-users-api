namespace postech.Users.Api.Application.DTOs;

public record UserResponse(
    Guid Id,
    string Email,
    string Name,
    string Role,
    DateTime CreatedAt
);