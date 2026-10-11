using System.Security.Claims;
using Eshhanly.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Eshhanly.Services;

public record RegisterModel
{
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Driver;
    public HostKind HostKind { get; set; } = HostKind.Individual;
    public string Governorate { get; set; } = "دمشق";
    public string? CarModel { get; set; }
}

/// <summary>
/// Credentials are checked inside the interactive page, which then redirects the browser to
/// /account/signin?t={ticket}. That plain HTTP request is where the auth cookie gets written.
/// </summary>
public class AuthService(
    IDbContextFactory<AppDbContext> dbFactory,
    IDataProtectionProvider dp,
    NotificationService notifications)
{
    private static readonly PasswordHasher<User> Hasher = new();
    private ITimeLimitedDataProtector Protector => dp.CreateProtector("signin-ticket").ToTimeLimitedDataProtector();

    public async Task<(User? User, string? Error)> ValidateAsync(string phoneInput, string password)
    {
        var phone = PhoneNumber.Normalize(phoneInput);
        if (phone is null) return (null, "رقم الموبايل غير صحيح");

        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        if (user is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            await Task.Delay(1000); // slows down password guessing
            return (null, "رقم الموبايل أو كلمة المرور غير صحيحة");
        }
        if (user.IsBlocked) return (null, "هذا الحساب محظور، تواصل مع الإدارة");
        return (user, null);
    }

    public async Task<(User? User, string? Error)> RegisterAsync(RegisterModel m)
    {
        var phone = PhoneNumber.Normalize(m.Phone);
        if (phone is null) return (null, "رقم الموبايل غير صحيح (مثال: 0912345678)");
        if (m.Role == UserRole.Admin) return (null, "نوع حساب غير مسموح");

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Phone == phone))
            return (null, "هذا الرقم مسجل مسبقاً، سجّل الدخول بدلاً من ذلك");

        var user = new User
        {
            FullName = m.FullName.Trim(),
            Phone = phone,
            Role = m.Role,
            HostKind = m.Role == UserRole.Host ? m.HostKind : null,
            Governorate = m.Governorate,
            CarModel = m.Role == UserRole.Driver ? m.CarModel?.Trim() : null,
            SubscriptionRequestedAt = DateTime.UtcNow, // registering = submitting a subscription request
        };
        user.PasswordHash = Hasher.HashPassword(user, m.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await notifications.NotifyAdminsAsync("طلب اشتراك جديد", $"{user.FullName} ({user.Role.Label()}) - {user.Phone}", "/admin");
        return (user, null);
    }

    public async Task<string?> ChangePasswordAsync(int userId, string current, string next)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return "المستخدم غير موجود";
        if (Hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
            return "كلمة المرور الحالية غير صحيحة";
        user.PasswordHash = Hasher.HashPassword(user, next);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> ResetPasswordAsync(int userId, string newPassword)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return "المستخدم غير موجود";
        user.PasswordHash = Hasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync();
        return null;
    }

    public string CreateTicket(int userId, bool remember) =>
        Protector.Protect($"{userId}|{remember}", TimeSpan.FromSeconds(30));

    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/account/signin", async (string t, string? returnUrl, HttpContext http, AuthService auth,
            IDbContextFactory<AppDbContext> dbFactory) =>
        {
            string payload;
            try { payload = auth.Protector.Unprotect(t); }
            catch { return Results.Redirect("/login?error=expired"); }

            var parts = payload.Split('|');
            await using var db = await dbFactory.CreateDbContextAsync();
            var user = await db.Users.FindAsync(int.Parse(parts[0]));
            if (user is null || user.IsBlocked) return Results.Redirect("/login");

            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
            ], CookieAuthenticationDefaults.AuthenticationScheme);

            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = parts[1] == "True" });

            var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
                ? returnUrl : "/";
            return Results.Redirect(target);
        });

        app.MapPost("/account/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });
    }
}
