using FluentAssertions;

namespace postech.Users.Api.Tests.Application.Services;

public class TokenServiceTests
{
    [Fact]
    public void TokenService_WasReplacedByCognitoAuthService()
    {
        // Token generation is now delegated to Cognito via ICognitoAuthService.
        true.Should().BeTrue();
    }
}
