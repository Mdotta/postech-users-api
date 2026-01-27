namespace postech.Users.Api.Application.DTOs;

public record RegisterUserRequest(
  string Email,
  string Name,
  string Password,
  string? Role = null // default será "User"
);