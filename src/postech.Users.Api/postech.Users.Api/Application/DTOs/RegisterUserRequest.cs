using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Application.DTOs;

public record RegisterUserRequest(
  string Email,
  string Name,
  string Password,
  UserRoles? Role = null // default será UserRoles.User
);