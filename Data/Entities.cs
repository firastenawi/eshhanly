namespace Eshhanly.Data;

public enum UserRole
{
    Admin = 0,
    Driver = 1,   // صاحب سيارة
    Host = 2,     // صاحب نقطة شحن
}

public enum HostKind
{
    Individual = 0, // فرد
    Station = 1,    // محطة
}

public enum PointStatus
{
    Active = 0,       // فعالة (متاحة)
    InUse = 1,        // قيد الاستخدام
    Inactive = 2,     // غير فعالة
    Maintenance = 3,  // صيانة
}

public enum RequestStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3,
}

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";          // 09xxxxxxxx — used for login
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public HostKind? HostKind { get; set; }
    public string Governorate { get; set; } = "";
    public string? CarModel { get; set; }

    /// <summary>Subscription is active while this is in the future. Set manually by an admin after cash payment.</summary>
    public DateTime? ActiveUntil { get; set; }

    /// <summary>Set when the user asks for (re)activation; cleared when an admin activates.</summary>
    public DateTime? SubscriptionRequestedAt { get; set; }

    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ChargePoint> ChargePoints { get; set; } = [];

    public bool IsActive(DateTime nowUtc) =>
        Role == UserRole.Admin || (!IsBlocked && ActiveUntil > nowUtc);
}

public class SubscriptionPeriod
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public long? AmountPaid { get; set; }
    public string? Note { get; set; }
    public int? ActivatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ChargePoint
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Governorate { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public string? MapUrl { get; set; }
    public string Connectors { get; set; } = "";     // comma separated, e.g. "Type 2,CCS2"
    public decimal? PowerKw { get; set; }
    public string? PriceNote { get; set; }           // free text, payment is cash between the parties
    public string? WorkingHours { get; set; }
    public string? Notes { get; set; }
    public PointStatus Status { get; set; } = PointStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChargeRequest
{
    public int Id { get; set; }
    public int ChargePointId { get; set; }
    public ChargePoint ChargePoint { get; set; } = null!;
    public int DriverId { get; set; }
    public User Driver { get; set; } = null!;
    public int HostId { get; set; }
    public User Host { get; set; } = null!;
    public string? Message { get; set; }
    public string? CarInfo { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string? ResponseMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];

    public bool IsChatOpen => Status is RequestStatus.Pending or RequestStatus.Accepted;
}

public class ChatMessage
{
    public int Id { get; set; }
    public int ChargeRequestId { get; set; }
    public ChargeRequest ChargeRequest { get; set; } = null!;
    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Single-row table (Id = 1) holding admin-editable settings.</summary>
public class AppSettings
{
    public int Id { get; set; } = 1;
    public long MonthlyFee { get; set; } = 50000;
    public string PaymentInstructions { get; set; } =
        "ادفع قيمة الاشتراك نقداً لإدارة التطبيق، وبعدها يتم تفعيل حسابك خلال 24 ساعة.";
    public string? ContactPhone { get; set; }
}
