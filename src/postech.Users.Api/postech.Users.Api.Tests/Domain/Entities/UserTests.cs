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
    
    [Theory]
        [InlineData("Pass1234", false)]
        [InlineData("SecureP@ss123", true)]
        [InlineData("MyP@ssw0rd", true)]
        [InlineData("short1!", false)] // Less than 8 chars
        [InlineData("nouppercase123!", false)] // No uppercase
        [InlineData("NOLOWERCASE123!", false)] // No lowercase
        [InlineData("NoDigitHere!", false)] // No digit
        [InlineData("NoSpecialChar123", false)] // No special character
        [InlineData("", false)] // Empty string
        public void IsSafePassword_ShouldReturnExpectedResult(string password, bool expected)
        {
            // Act
            var result = User.IsSafePassword(password);
    
            // Assert
            result.Should().Be(expected);
        }
    
        [Fact]
        public void HashPassword_ShouldReturnHashedPassword()
        {
            // Arrange
            var password = "ValidP@ssw0rd123";
    
            // Act
            var hashedPassword = User.HashPassword(password);
    
            // Assert
            hashedPassword.Should().NotBeEmpty();
            hashedPassword.Should().NotBe(password);
        }
    
        [Fact]
        public void HashPassword_ShouldCreateDifferentHashesForSamePassword()
        {
            // Arrange
            var password = "ValidP@ssw0rd123";
    
            // Act
            var hash1 = User.HashPassword(password);
            var hash2 = User.HashPassword(password);
    
            // Assert
            hash1.Should().NotBe(hash2);
        }
    
        [Fact]
        public void VerifyPassword_ShouldReturnTrueForCorrectPassword()
        {
            // Arrange
            var password = "ValidP@ssw0rd123";
            var hashedPassword = User.HashPassword(password);
            var user = new User("test@email.com", "Test User", hashedPassword);
    
            // Act
            var result = user.VerifyPassword(password);
    
            // Assert
            result.Should().BeTrue();
        }
    
        [Fact]
        public void VerifyPassword_ShouldReturnFalseForIncorrectPassword()
        {
            // Arrange
            var password = "ValidP@ssw0rd123";
            var wrongPassword = "WrongP@ssw0rd456";
            var hashedPassword = User.HashPassword(password);
            var user = new User("test@email.com", "Test User", hashedPassword);
    
            // Act
            var result = user.VerifyPassword(wrongPassword);
    
            // Assert
            result.Should().BeFalse();
        }
    
        [Fact]
        public void UpdateRole_ShouldChangeUserRole()
        {
            // Arrange
            var user = new User("test@email.com", "Test User", "hashed_pass", UserRoles.User);
            var initialUpdatedAt = user.UpdatedAt;
    
            // Act
            System.Threading.Thread.Sleep(10); // Ensure time difference
            user.UpdateRole(UserRoles.Administrator);
    
            // Assert
            user.Role.Should().Be(UserRoles.Administrator);
            user.UpdatedAt.Should().BeAfter(initialUpdatedAt);
        }
    
        [Fact]
        public void CreateUserWithAdminRole_ShouldInitializeWithCorrectRole()
        {
            // Arrange & Act
            var user = new User("admin@email.com", "Admin User", "hashed_pass", UserRoles.Administrator);
    
            // Assert
            user.Role.Should().Be(UserRoles.Administrator);
        }
    
        [Fact]
        public void CreateUser_ShouldSetTimestamps()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;
    
            // Act
            var user = new User("test@email.com", "Test User", "hashed_pass");
            var afterCreation = DateTime.UtcNow;
    
            // Assert
            user.CreatedAt.Should().BeOnOrAfter(beforeCreation).And.BeOnOrBefore(afterCreation);
            user.UpdatedAt.Should().BeOnOrAfter(beforeCreation).And.BeOnOrBefore(afterCreation);
        }
    
}

