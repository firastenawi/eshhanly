using Eshhanly.Data;
using Microsoft.EntityFrameworkCore;

namespace Eshhanly.Services;

public class NotificationService(IDbContextFactory<AppDbContext> dbFactory, LiveUpdates live)
{
    public async Task NotifyAsync(int userId, string title, string? body, string? link)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Notifications.Add(new Notification { UserId = userId, Title = title, Body = body, Link = link });
        await db.SaveChangesAsync();
        live.Notify(userId);
    }

    public async Task NotifyAdminsAsync(string title, string? body, string? link)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var adminIds = await db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync();
        db.Notifications.AddRange(adminIds.Select(id => new Notification { UserId = id, Title = title, Body = body, Link = link }));
        await db.SaveChangesAsync();
        live.Notify([.. adminIds]);
    }

    public async Task<(int Notifications, int Messages)> UnreadCountsAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var n = await db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead);
        var m = await db.ChatMessages.CountAsync(x => !x.IsRead && x.SenderId != userId &&
            (x.ChargeRequest.DriverId == userId || x.ChargeRequest.HostId == userId));
        return (n, m);
    }

    public async Task<List<Notification>> ListAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Notifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Id).Take(100).ToListAsync();
    }

    public async Task MarkAllReadAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));
        live.Notify(userId);
    }
}
