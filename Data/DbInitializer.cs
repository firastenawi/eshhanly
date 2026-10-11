using Eshhanly.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Eshhanly.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        if (db.Database.IsNpgsql())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (!await db.Settings.AnyAsync())
            db.Settings.Add(new AppSettings());

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            var phone = PhoneNumber.Normalize(config["Admin:Phone"] ?? "");
            var password = config["Admin:Password"];
            if (phone is null || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("No admin account exists. Set Admin__Phone and Admin__Password to create one.");
            }
            else
            {
                var admin = new User
                {
                    FullName = config["Admin:Name"] ?? "مدير التطبيق",
                    Phone = phone,
                    Role = UserRole.Admin,
                    Governorate = "دمشق",
                };
                admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);
                db.Users.Add(admin);
                logger.LogInformation("Admin account created for {Phone}", phone);
            }
        }

        await db.SaveChangesAsync();
    }
}
