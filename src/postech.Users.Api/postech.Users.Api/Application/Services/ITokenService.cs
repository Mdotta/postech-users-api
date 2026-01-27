using postech.Users.Api.Domain.Entities;

namespace postech.Users.Api.Application.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}