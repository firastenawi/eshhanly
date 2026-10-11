using Eshhanly.Components;
using Eshhanly.Data;
using Eshhanly.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms (Render, Railway, Koyeb...) give the port in $PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---------- Database: PostgreSQL in production, SQLite file for local development ----------
var pg = builder.Configuration.GetConnectionString("Postgres");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL"); // postgres://user:pass@host/db
if (string.IsNullOrWhiteSpace(pg) && !string.IsNullOrWhiteSpace(databaseUrl))
    pg = DatabaseUrl.ToNpgsql(databaseUrl);

builder.Services.AddDbContextFactory<AppDbContext>(o =>
{
    if (!string.IsNullOrWhiteSpace(pg)) o.UseNpgsql(pg);
    else o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=eshhanly.db");
});

// Keys must survive restarts, otherwise everyone gets logged out on each deploy.
builder.Services.AddDataProtection()
    .SetApplicationName("Eshhanly")
    .PersistKeysToDbContext<AppDbContext>();

// ---------- Auth ----------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.LogoutPath = "/account/logout";
        o.AccessDeniedPath = "/subscription";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Cookie.Name = "eshhanly.auth";
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ActiveSubscriptionHandler.Policy, p => p.RequireAuthenticatedUser().AddRequirements(new ActiveSubscriptionRequirement()));
builder.Services.AddSingleton<IAuthorizationHandler, ActiveSubscriptionHandler>();
builder.Services.AddCascadingAuthenticationState();

// ---------- App services ----------
builder.Services.AddSingleton<LiveUpdates>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PointService>();
builder.Services.AddScoped<RequestService>();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
AuthService.MapEndpoints(app);
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
