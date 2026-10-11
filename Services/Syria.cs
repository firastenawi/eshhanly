using Eshhanly.Data;

namespace Eshhanly.Services;

public static class Syria
{
    public static readonly string[] Governorates =
    [
        "دمشق", "ريف دمشق", "حلب", "حمص", "حماة", "اللاذقية", "طرطوس",
        "إدلب", "درعا", "السويداء", "القنيطرة", "دير الزور", "الرقة", "الحسكة",
    ];

    public static readonly string[] ConnectorTypes =
        ["Type 2", "CCS2", "GB/T AC", "GB/T DC", "CHAdeMO", "Type 1", "مقبس منزلي عادي"];

    // Syria is on UTC+3 all year (since 2022).
    private static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    public static DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc) + Offset;

    public static string Format(DateTime utc) => ToLocal(utc).ToString("yyyy/MM/dd hh:mm tt",
        System.Globalization.CultureInfo.GetCultureInfo("ar-SY"));

    public static string FormatDate(DateTime utc) => ToLocal(utc).ToString("yyyy/MM/dd");

    public static string Money(long amount) => $"{amount:N0} ل.س";

    public static string Label(this UserRole r) => r switch
    {
        UserRole.Admin => "مدير",
        UserRole.Driver => "صاحب سيارة",
        UserRole.Host => "صاحب نقطة شحن",
        _ => r.ToString(),
    };

    public static string Label(this HostKind k) => k switch
    {
        HostKind.Individual => "فرد",
        HostKind.Station => "محطة شحن",
        _ => k.ToString(),
    };

    public static string Label(this PointStatus s) => s switch
    {
        PointStatus.Active => "فعّالة",
        PointStatus.InUse => "قيد الاستخدام",
        PointStatus.Inactive => "غير فعّالة",
        PointStatus.Maintenance => "صيانة",
        _ => s.ToString(),
    };

    public static MudBlazor.Color Color(this PointStatus s) => s switch
    {
        PointStatus.Active => MudBlazor.Color.Success,
        PointStatus.InUse => MudBlazor.Color.Warning,
        PointStatus.Maintenance => MudBlazor.Color.Info,
        _ => MudBlazor.Color.Default,
    };

    public static bool AcceptsRequests(this PointStatus s) => s is PointStatus.Active or PointStatus.InUse;

    public static string Label(this RequestStatus s) => s switch
    {
        RequestStatus.Pending => "بانتظار الرد",
        RequestStatus.Accepted => "مقبول",
        RequestStatus.Rejected => "مرفوض",
        RequestStatus.Cancelled => "ملغى",
        _ => s.ToString(),
    };

    public static MudBlazor.Color Color(this RequestStatus s) => s switch
    {
        RequestStatus.Pending => MudBlazor.Color.Warning,
        RequestStatus.Accepted => MudBlazor.Color.Success,
        RequestStatus.Rejected => MudBlazor.Color.Error,
        _ => MudBlazor.Color.Default,
    };

    /// <summary>Human readable subscription state.</summary>
    public static (string Text, MudBlazor.Color Color) SubscriptionState(User u, DateTime nowUtc)
    {
        if (u.Role == UserRole.Admin) return ("مدير", MudBlazor.Color.Primary);
        if (u.IsBlocked) return ("محظور", MudBlazor.Color.Error);
        if (u.ActiveUntil > nowUtc) return ("فعّال", MudBlazor.Color.Success);
        if (u.SubscriptionRequestedAt is not null) return ("بانتظار التفعيل", MudBlazor.Color.Warning);
        return u.ActiveUntil is null ? ("غير مفعّل", MudBlazor.Color.Default) : ("منتهي", MudBlazor.Color.Error);
    }
}

public static class PhoneNumber
{
    /// <summary>Accepts 09xxxxxxxx, 9xxxxxxxx, +9639xxxxxxxx, 009639xxxxxxxx. Returns 09xxxxxxxx or null.</summary>
    public static string? Normalize(string input)
    {
        var digits = new string((input ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00963")) digits = digits[5..];
        else if (digits.StartsWith("963")) digits = digits[3..];
        if (digits.StartsWith('9') && digits.Length == 9) digits = "0" + digits;
        return digits.Length == 10 && digits.StartsWith("09") ? digits : null;
    }
}
