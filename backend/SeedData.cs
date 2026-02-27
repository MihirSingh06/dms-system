using backend;
using backend.Models;

public static class SeedData
{
    public static void SeedUsers(AppDbContext db)
    {
        if (db.Users.Any()) return;

        db.Users.AddRange(
            new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Finance"
            },
            new User
            {
                Username = "manager",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                Role = "Manager"
            },
            new User
            {
                Username = "reviewer",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("reviewer123"),
                Role = "Reviewer"
            }
        );

        db.SaveChanges();
    }
}