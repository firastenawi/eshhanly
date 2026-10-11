using Eshhanly.Data;
using Microsoft.EntityFrameworkCore;

namespace Eshhanly.Services;

public record PointFilter(string? Governorate, string? Connector, bool AvailableOnly, string? Search);

public class PointService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>Points visible to drivers: owner subscription active and not blocked.</summary>
    public async Task<List<ChargePoint>> SearchAsync(PointFilter f)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var q = db.ChargePoints.AsNoTracking().Include(p => p.Owner)
            .Where(p => !p.Owner.IsBlocked && p.Owner.ActiveUntil > now);

        if (!string.IsNullOrEmpty(f.Governorate)) q = q.Where(p => p.Governorate == f.Governorate);
        if (!string.IsNullOrEmpty(f.Connector)) q = q.Where(p => p.Connectors.Contains(f.Connector));
        if (f.AvailableOnly) q = q.Where(p => p.Status == PointStatus.Active);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(p => p.Name.Contains(s) || p.City.Contains(s) || p.Address.Contains(s));
        }

        return await q.OrderBy(p => p.Status).ThenByDescending(p => p.UpdatedAt).Take(200).ToListAsync();
    }

    public async Task<ChargePoint?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChargePoints.AsNoTracking().Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<ChargePoint>> MineAsync(int ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChargePoints.AsNoTracking().Include(p => p.Owner).Where(p => p.OwnerId == ownerId).OrderBy(p => p.Id).ToListAsync();
    }

    public async Task<List<ChargePoint>> AllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChargePoints.AsNoTracking().Include(p => p.Owner).OrderByDescending(p => p.Id).Take(500).ToListAsync();
    }

    public async Task SaveAsync(ChargePoint input, int ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        ChargePoint point;
        if (input.Id == 0)
        {
            point = new ChargePoint { OwnerId = ownerId };
            db.ChargePoints.Add(point);
        }
        else
        {
            point = await db.ChargePoints.FirstOrDefaultAsync(p => p.Id == input.Id && p.OwnerId == ownerId)
                ?? throw new InvalidOperationException("النقطة غير موجودة");
        }

        point.Name = input.Name.Trim();
        point.Governorate = input.Governorate;
        point.City = input.City.Trim();
        point.Address = input.Address.Trim();
        point.MapUrl = string.IsNullOrWhiteSpace(input.MapUrl) ? null : input.MapUrl.Trim();
        point.Connectors = input.Connectors;
        point.PowerKw = input.PowerKw;
        point.PriceNote = input.PriceNote?.Trim();
        point.WorkingHours = input.WorkingHours?.Trim();
        point.Notes = input.Notes?.Trim();
        point.Status = input.Status;
        point.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SetStatusAsync(int pointId, int ownerId, PointStatus status)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ChargePoints.Where(p => p.Id == pointId && p.OwnerId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status).SetProperty(p => p.UpdatedAt, DateTime.UtcNow));
    }

    public async Task DeleteAsync(int pointId, int ownerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var point = await db.ChargePoints.FirstOrDefaultAsync(p => p.Id == pointId && p.OwnerId == ownerId);
        if (point is null) return;
        db.ChargePoints.Remove(point);
        await db.SaveChangesAsync();
    }
}
