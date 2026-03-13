namespace postech.Users.Api.Application.DTOs;

public record LoginRequest(
    string Email,
    string Password
);