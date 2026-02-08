using FluentAssertions;
using postech.Users.Api.Application.DTOs;
using postech.Users.Api.Application.Validations;
using postech.Users.Api.Domain.Enums;
using postech.Users.Api.Domain.Errors;

namespace postech.Users.Api.Tests.Application.Validations;

public class RegisterUserRequestValidatorTests
{
    [Fact]
    public void Validate_ShouldPassForValidRequest()
    {
        // Arrange
        var request = new RegisterUserRequest("valid@email.com", "Valid User", "StrongP@ssw0rd!", UserRoles.User);
        
        // Act
        var result = RegisterUserRequestValidator.Validate(request);
        
        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldReturnExpectedErrors()
    {
        // Arrange
        var request = new RegisterUserRequest("valid@email", "", "weakPass", UserRoles.User);
        
        // Act
        var result = RegisterUserRequestValidator.Validate(request);
        
        // Assert
        result.Errors.Should().Contain(Errors.User.UnsafePassword);
        result.Errors.Should().Contain(Errors.User.InvalidEmail);
        result.Errors.Should().Contain(Errors.User.NameRequired);
    }
}