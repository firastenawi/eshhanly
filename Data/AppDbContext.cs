using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eshhanly.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SubscriptionPeriod> SubscriptionPeriods => Set<SubscriptionPeriod>();
    public DbSet<ChargePoint> ChargePoints => Set<ChargePoint>();
    public DbSet<ChargeRequest> ChargeRequests => Set<ChargeRequest>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Governorate).HasMaxLength(50);
            e.Property(x => x.CarModel).HasMaxLength(100);
        });

        b.Entity<SubscriptionPeriod>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Note).HasMaxLength(500);
        });

        b.Entity<ChargePoint>(e =>
        {
            e.HasOne(x => x.Owner).WithMany(u => u.ChargePoints).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Governorate, x.Status });
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Governorate).HasMaxLength(50);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.MapUrl).HasMaxLength(500);
            e.Property(x => x.Connectors).HasMaxLength(200);
            e.Property(x => x.PowerKw).HasPrecision(6, 1);
            e.Property(x => x.PriceNote).HasMaxLength(200);
            e.Property(x => x.WorkingHours).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        b.Entity<ChargeRequest>(e =>
        {
            e.HasOne(x => x.ChargePoint).WithMany().HasForeignKey(x => x.ChargePointId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Host).WithMany().HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.HostId, x.Status });
            e.HasIndex(x => new { x.DriverId, x.Status });
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.CarInfo).HasMaxLength(100);
            e.Property(x => x.ResponseMessage).HasMaxLength(500);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.HasOne(x => x.ChargeRequest).WithMany(r => r.Messages).HasForeignKey(x => x.ChargeRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Body).HasMaxLength(1000);
        });

        b.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Body).HasMaxLength(500);
            e.Property(x => x.Link).HasMaxLength(200);
        });

        b.Entity<AppSettings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.PaymentInstructions).HasMaxLength(1000);
            e.Property(x => x.ContactPhone).HasMaxLength(20);
        });
    }
}

/// <summary>Used only by `dotnet ef` — migrations target PostgreSQL (production).</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=eshhanly;Username=postgres;Password=postgres")
            .Options);
}
