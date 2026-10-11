using Eshhanly.Data;
using Microsoft.EntityFrameworkCore;

namespace Eshhanly.Services;

public record UserRow(User User, int PointsCount);

public record AdminStats(int Drivers, int Hosts, int ActiveUsers, int PendingRequests, int Points, int ActivePoints,
    int ChargeRequests, int AcceptedRequests, long RevenueThisMonth);

public class SubscriptionService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, LiveUpdates live)
{
    public async Task<AppSettings> GetSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings s)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        s.Id = 1;
        db.Settings.Update(s);
        await db.SaveChangesAsync();
    }

    /// <summary>User asks the admin to (re)activate their account after paying cash.</summary>
    public async Task RequestActivationAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException();
        if (user.SubscriptionRequestedAt is not null) return;
        user.SubscriptionRequestedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await notifications.NotifyAdminsAsync("طلب تجديد اشتراك", $"{user.FullName} - {user.Phone}", "/admin");
    }

    /// <summary>
    /// Activates for <paramref name="days"/> days. If still active, the new period is appended to the current one.
    /// Deactivation is automatic: once ActiveUntil passes, the account is treated as inactive everywhere.
    /// </summary>
    public async Task ActivateAsync(int userId, int days, long? amountPaid, string? note, int adminId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException();
        var now = DateTime.UtcNow;
        var start = user.ActiveUntil > now ? user.ActiveUntil.Value : now;
        var end = start.AddDays(days);

        user.ActiveUntil = end;
        user.SubscriptionRequestedAt = null;
        db.SubscriptionPeriods.Add(new SubscriptionPeriod
        {
            UserId = userId, StartsAt = start, EndsAt = end, AmountPaid = amountPaid, Note = note, ActivatedById = adminId,
        });
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(userId, "تم تفعيل اشتراكك ✅",
            $"اشتراكك فعّال حتى {Syria.FormatDate(end)}", "/");
    }

    public async Task DeactivateAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException();
        user.ActiveUntil = DateTime.UtcNow;
        await db.SaveChangesAsync();
        live.Notify(userId);
    }

    public async Task RejectRequestAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException();
        user.SubscriptionRequestedAt = null;
        await db.SaveChangesAsync();
        await notifications.NotifyAsync(userId, "لم يتم تفعيل اشتراكك",
            "تواصل مع الإدارة لمزيد من التفاصيل", "/subscription");
    }

    public async Task SetBlockedAsync(int userId, bool blocked)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException();
        if (user.Role == UserRole.Admin) return;
        user.IsBlocked = blocked;
        await db.SaveChangesAsync();
        live.Notify(userId);
    }

    public async Task<List<SubscriptionPeriod>> HistoryAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SubscriptionPeriods.AsNoTracking().Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Id).ToListAsync();
    }

    public async Task<List<UserRow>> ListUsersAsync(string? search, UserRole? role, bool pendingOnly)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Users.AsNoTracking().Where(u => u.Role != UserRole.Admin);
        if (role is not null) q = q.Where(u => u.Role == role);
        if (pendingOnly) q = q.Where(u => u.SubscriptionRequestedAt != null && !u.IsBlocked);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.FullName.Contains(s) || u.Phone.Contains(s));
        }
        return await q
            .OrderByDescending(u => u.SubscriptionRequestedAt != null)
            .ThenBy(u => u.SubscriptionRequestedAt)
            .ThenByDescending(u => u.Id)
            .Select(u => new UserRow(u, u.ChargePoints.Count))
            .Take(500)
            .ToListAsync();
    }

    public async Task<AdminStats> StatsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var local = Syria.ToLocal(now);
        var monthStart = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-3);
        return new AdminStats(
            await db.Users.CountAsync(u => u.Role == UserRole.Driver),
            await db.Users.CountAsync(u => u.Role == UserRole.Host),
            await db.Users.CountAsync(u => u.Role != UserRole.Admin && !u.IsBlocked && u.ActiveUntil > now),
            await db.Users.CountAsync(u => u.SubscriptionRequestedAt != null && !u.IsBlocked),
            await db.ChargePoints.CountAsync(),
            await db.ChargePoints.CountAsync(p => p.Status == PointStatus.Active),
            await db.ChargeRequests.CountAsync(),
            await db.ChargeRequests.CountAsync(r => r.Status == RequestStatus.Accepted),
            await db.SubscriptionPeriods.Where(p => p.CreatedAt >= monthStart).SumAsync(p => p.AmountPaid ?? 0));
    }
}
