using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration cfg)
    {
        // Creates the tables if they do not exist yet (safe if you already ran sql/schema.sql)
        await db.Database.EnsureCreatedAsync();

        foreach (var name in new[] { RoleNames.Admin, RoleNames.Farmer, RoleNames.Technician })
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
                db.Roles.Add(new AppRole { Name = name });
        }
        await db.SaveChangesAsync();

        if (!await db.Users.AnyAsync())
        {
            var adminRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.Admin);
            var password = cfg["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password)) password = "Admin@12345";
            var email = cfg["Seed:AdminEmail"];
            if (string.IsNullOrWhiteSpace(email)) email = "admin@example.com";

            db.Users.Add(new AppUser
            {
                Username = "admin",
                Email = email,
                PasswordHash = PasswordHasher.Hash(password),
                RoleId = adminRole.Id
            });
            await db.SaveChangesAsync();
        }
    }
}
