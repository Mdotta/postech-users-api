using ErrorOr;

namespace postech.Users.Api.Domain.Errors;

public static partial class Errors
{
    public static class User
    {
        public static Error InvalidEmail => Error.Validation(
            code: "User.Email.Invalid",
            description: "Invalid email format.");

        public static Error UnsafePassword => Error.Validation(
            code: "User.Password.Unsafe",
            description: "Password does not meet safety requirements. Minimum 8 characters, including uppercase, lowercase, digit, and special character.");

        public static Error NameRequired => Error.Validation(
            code: "User.Name.Required",
            description: "Name is required.");

        public static Error EmailAlreadyExists => Error.Conflict(
            code: "User.Email.AlreadyExists",
            description: "Email already registered.");

        public static Error NotFound => Error.NotFound(
            code: "User.NotFound",
            description: "User not found.");

        public static Error InvalidCredentials => Error.Validation(
            code: "User.Credentials.Invalid",
            description: "Invalid email or password.");

        public static Error ForbiddenAdminCreation => Error.Forbidden(
            code: "User.ForbiddenAdminCreation",
            description: "Only administrators can create admin accounts.");
    }
}

