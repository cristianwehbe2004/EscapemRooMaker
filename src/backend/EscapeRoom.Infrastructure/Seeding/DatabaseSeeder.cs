using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Seeding;

public class DatabaseSeeder(AppDbContext dbContext, IPasswordHasher<User> passwordHasher)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var roleSeed = new[]
        {
            new { Name = "Creator", Description = "Can create and publish rooms." },
            new { Name = "Player", Description = "Can join and play sessions." },
            new { Name = "GM", Description = "Can moderate active sessions." },
            new { Name = "Admin", Description = "Can manage the global library and moderation." }
        };

        foreach (var role in roleSeed)
        {
            var exists = await dbContext.Roles.AnyAsync(x => x.Name == role.Name, cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.Roles.Add(new Role
            {
                Name = role.Name,
                Description = role.Description,
                IsSystem = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var defaultUsers = new[]
        {
            new { Username = "creator1", Email = "creator1@escaperoom.local", Password = "Creator123!", Role = UserRole.Creator },
            new { Username = "player1", Email = "player1@escaperoom.local", Password = "Player123!", Role = UserRole.Player },
            new { Username = "gm1", Email = "gm1@escaperoom.local", Password = "Gm123456!", Role = UserRole.GM },
            new { Username = "admin", Email = "admin@escaperoom.local", Password = "Admin123!", Role = UserRole.Admin }
        };

        foreach (var item in defaultUsers)
        {
            var existing = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == item.Email, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            var user = new User
            {
                Username = item.Username,
                Email = item.Email,
                Role = item.Role,
                CreatedAtUtc = DateTime.UtcNow
            };

            user.PasswordHash = passwordHasher.HashPassword(user, item.Password);
            dbContext.Users.Add(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!await dbContext.Rooms.AnyAsync(cancellationToken))
        {
            var creator = await dbContext.Users.FirstAsync(x => x.Role == UserRole.Creator, cancellationToken);
            dbContext.Rooms.Add(new Room
            {
                Name = "Vault Puzzle",
                Description = "Starter room used by seed script.",
                CreatedByUserId = creator.Id,
                IsPublished = true,
                GraphDefinition = "{}",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
