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
}