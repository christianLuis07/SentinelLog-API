using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;

namespace SentinelLog.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(
        SentinelLogDbContext context,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await context.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already seeded with users. Skipping seed.");
            return;
        }

        logger.LogInformation("Seeding initial development users with role-based credentials...");

        var users = new List<User>
        {
            new()
            {
                Username = "admin",
                Email = "admin@sentinellog.local",
                PasswordHash = passwordHasher.Hash("Admin123!#Sentinel"),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Username = "analyst",
                Email = "analyst@sentinellog.local",
                PasswordHash = passwordHasher.Hash("Analyst123!#Sentinel"),
                Role = UserRole.SecurityAnalyst,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Username = "service-agent",
                Email = "service@sentinellog.local",
                PasswordHash = passwordHasher.Hash("Service123!#Sentinel"),
                Role = UserRole.Service,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Username = "auditor-viewer",
                Email = "viewer@sentinellog.local",
                PasswordHash = passwordHasher.Hash("Viewer123!#Sentinel"),
                Role = UserRole.Viewer,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        await context.Users.AddRangeAsync(users, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} initial development users.", users.Count);
    }
}
