using ErrorOr;
using postech.Users.Api.Application.DTOs;
using postech.Users.Api.Domain.Entities;
using postech.Users.Api.Domain.Errors;

namespace postech.Users.Api.Application.Validations;

public static class RegisterUserRequestValidator
{
    public static ErrorOr<Success> Validate(RegisterUserRequest request)
    {
        List<Error> errors = new List<Error>();
        
        if (!User.IsSafePassword(request.Password))
        {
            errors.Add(Errors.User.UnsafePassword);
        }

        if (!User.IsValidEmail(request.Email))
        {
            errors.Add(Errors.User.InvalidEmail);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(Errors.User.NameRequired);
        }
        
        if (errors.Any())
        {
            return errors;
        }
        
        return Result.Success;
    }
}