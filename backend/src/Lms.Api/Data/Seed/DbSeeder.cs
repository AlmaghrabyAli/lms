using Lms.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lms.Api.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        // No HTTP request at startup, so we create a scope manually (DbContext is scoped).
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync();

        await EnsureUserAsync(db, logger, "Platform Admin",
            config["Seed:AdminEmail"], config["Seed:AdminPassword"], Roles.Admin);

        if (config.GetValue<bool>("Seed:DemoData"))
        {
            var demoPassword = config["Seed:DemoPassword"];
            await EnsureUserAsync(db, logger, "Demo Instructor", "instructor@lms.local", demoPassword, Roles.Instructor);
            await EnsureUserAsync(db, logger, "Demo Student", "student@lms.local", demoPassword, Roles.Student);
        }
    }

    private static async Task EnsureUserAsync(
        AppDbContext db, ILogger logger, string fullName, string? email, string? password, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed skipped for role {Role}: email or password is not configured.", role);
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            return;
        }

        db.Users.Add(new User
        {
            FullName = fullName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Role} user {Email}", role, normalizedEmail);
    }
}