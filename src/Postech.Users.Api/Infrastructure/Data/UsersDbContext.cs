using Microsoft.EntityFrameworkCore;
using postech.Users.Api.Domain.Entities;

namespace postech.Users.Api.Infrastructure.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                
                entity.Property(e => e.Role)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);
            }
        );
    }
}