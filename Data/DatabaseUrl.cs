namespace Eshhanly.Data;

public static class DatabaseUrl
{
    /// <summary>Converts postgres://user:pass@host:port/db?sslmode=require (Neon/Render style) to an Npgsql connection string.</summary>
    public static string ToNpgsql(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var sslRequired = !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Query.Contains("sslmode=require");
        return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.AbsolutePath.TrimStart('/')};" +
               $"Username={Uri.UnescapeDataString(userInfo[0])};Password={Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "")};" +
               (sslRequired ? "SSL Mode=Require;" : "");
    }
}
