using postech.Users.Api.Domain.Enums;

namespace postech.Users.Api.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRoles Role { get; set; } = UserRoles.User;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    
    // EF Constructor
    private User() { }

    public User(string email, string name, string passwordHash, UserRoles role = UserRoles.User)
    {
        Id = Guid.NewGuid();
        Email = email;
        Name = name;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void UpdateRole(UserRoles roles)
    {
        Role = roles;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool VerifyPassword(string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(passwordHash, PasswordHash);
    }

    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
    
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        
        var regex = new System.Text.RegularExpressions.Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    
        return regex.IsMatch(email);
    }
    
    public static bool IsSafePassword(string password)
    {
        if (password.Length < 8)
            return false;
        if (!password.Any(char.IsUpper))
            return false;
        if (!password.Any(char.IsLower))
            return false;
        if (!password.Any(char.IsDigit))
            return false;
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            return false;
        return true;
    }
}