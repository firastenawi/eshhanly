using Eshhanly.Data;
using Microsoft.EntityFrameworkCore;

namespace Eshhanly.Services;

public record RequestRow(ChargeRequest Request, int Unread);

/// <summary>The whole job of the app: driver asks → host accepts/rejects → they chat. Nothing else.</summary>
public class RequestService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, LiveUpdates live)
{
    public async Task<(int? Id, string? Error)> CreateAsync(int driverId, int pointId, string? message, string? carInfo)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var driver = await db.Users.FindAsync(driverId);
        if (driver is null || driver.Role != UserRole.Driver || !driver.IsActive(now))
            return (null, "يجب أن يكون اشتراكك فعّالاً لإرسال طلب");

        var point = await db.ChargePoints.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == pointId);
        if (point is null || !point.Owner.IsActive(now)) return (null, "نقطة الشحن غير متاحة حالياً");
        if (!point.Status.AcceptsRequests()) return (null, $"لا يمكن الطلب، حالة النقطة: {point.Status.Label()}");
        if (await db.ChargeRequests.AnyAsync(r => r.DriverId == driverId && r.ChargePointId == pointId && r.Status == RequestStatus.Pending))
            return (null, "لديك طلب سابق بانتظار الرد على هذه النقطة");

        var req = new ChargeRequest
        {
            ChargePointId = pointId, DriverId = driverId, HostId = point.OwnerId,
            Message = message?.Trim(), CarInfo = carInfo?.Trim(),
        };
        db.ChargeRequests.Add(req);
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(point.OwnerId, "طلب شحن جديد ⚡",
            $"{driver.FullName} يطلب الشحن في «{point.Name}»", $"/requests/{req.Id}");
        return (req.Id, null);
    }

    public async Task<string?> RespondAsync(int requestId, int hostId, bool accept, string? response)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var req = await db.ChargeRequests.Include(r => r.ChargePoint).FirstOrDefaultAsync(r => r.Id == requestId && r.HostId == hostId);
        if (req is null) return "الطلب غير موجود";
        if (req.Status != RequestStatus.Pending) return "تم الرد على هذا الطلب مسبقاً";

        req.Status = accept ? RequestStatus.Accepted : RequestStatus.Rejected;
        req.ResponseMessage = string.IsNullOrWhiteSpace(response) ? null : response.Trim();
        req.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(req.DriverId,
            accept ? "تم قبول طلب الشحن ✅" : "تم رفض طلب الشحن ❌",
            $"«{req.ChargePoint.Name}»" + (req.ResponseMessage is null ? "" : $": {req.ResponseMessage}"),
            $"/requests/{req.Id}");
        return null;
    }

    public async Task<string?> CancelAsync(int requestId, int driverId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var req = await db.ChargeRequests.Include(r => r.ChargePoint).FirstOrDefaultAsync(r => r.Id == requestId && r.DriverId == driverId);
        if (req is null) return "الطلب غير موجود";
        if (req.Status is not (RequestStatus.Pending or RequestStatus.Accepted)) return "لا يمكن إلغاء هذا الطلب";
        req.Status = RequestStatus.Cancelled;
        await db.SaveChangesAsync();
        await notifications.NotifyAsync(req.HostId, "تم إلغاء طلب شحن", $"«{req.ChargePoint.Name}»", $"/requests/{req.Id}");
        return null;
    }

    /// <summary>Requests the user is part of (as driver or host).</summary>
    public async Task<List<RequestRow>> ListAsync(int userId, RequestStatus? status)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.ChargeRequests.AsNoTracking()
            .Include(r => r.ChargePoint).Include(r => r.Driver).Include(r => r.Host)
            .Where(r => r.DriverId == userId || r.HostId == userId);
        if (status is not null) q = q.Where(r => r.Status == status);
        return await q.OrderByDescending(r => r.Id).Take(200)
            .Select(r => new RequestRow(r, r.Messages.Count(m => !m.IsRead && m.SenderId != userId)))
            .ToListAsync();
    }

    /// <summary>Returns the request only if the user is one of its two parties (or an admin).</summary>
    public async Task<ChargeRequest?> GetAsync(int requestId, int userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var req = await db.ChargeRequests.AsNoTracking()
            .Include(r => r.ChargePoint).Include(r => r.Driver).Include(r => r.Host)
            .Include(r => r.Messages.OrderBy(m => m.Id))
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == requestId);
        return req is not null && (isAdmin || req.DriverId == userId || req.HostId == userId) ? req : null;
    }

    public async Task MarkReadAsync(int requestId, int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var n = await db.ChatMessages
            .Where(m => m.ChargeRequestId == requestId && m.SenderId != userId && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        if (n > 0) live.Notify(userId);
    }

    public async Task<string?> SendMessageAsync(int requestId, int senderId, string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        await using var db = await dbFactory.CreateDbContextAsync();
        var req = await db.ChargeRequests.FirstOrDefaultAsync(r => r.Id == requestId && (r.DriverId == senderId || r.HostId == senderId));
        if (req is null) return "المحادثة غير موجودة";
        if (!req.IsChatOpen) return "المحادثة مغلقة لأن الطلب " + req.Status.Label();

        db.ChatMessages.Add(new ChatMessage { ChargeRequestId = requestId, SenderId = senderId, Body = body.Trim() });
        await db.SaveChangesAsync();
        live.Notify(req.DriverId, req.HostId);
        return null;
    }
}
