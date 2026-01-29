using FluentAssertions;
using postech.Users.Api.Domain.Entities;
using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Tests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void CreateUser_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var name = "John Doe";
        var email = "john.doe@email.com";
        var hashedPassword = "hashed_password_123";


        // Act
        var user = new User(email, name, hashedPassword);

        // Assert
        user.Name.Should().Be(name);
        user.Email.Should().Be(email);
        user.PasswordHash.Should().Be(hashedPassword);
        user.Role.Should().Be(UserRoles.User);
    }

    [Theory]
    [InlineData("user@email.com", true)]
    [InlineData("test@domain.co.uk", true)]
    [InlineData("name@server.org", true)]
    [InlineData("invalid@email", false)]
    [InlineData("user @email.com", false)]
    [InlineData("user@.com", false)]
    [InlineData("@email.com", false)]
    public void IsEmailValid_ShouldReturnExpectedResult(string email, bool expected)
    {
        // Act
        var result = User.IsValidEmail(email);

        // Assert
        result.Should().Be(expected);
    }
}

