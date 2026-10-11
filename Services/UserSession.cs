using System.Security.Claims;
using Eshhanly.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Eshhanly.Services;

/// <summary>Per-circuit access to the signed-in user (always re-read from the database).</summary>
public class UserSession(AuthenticationStateProvider auth, IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<int?> GetUserIdAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        return int.TryParse(state.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    public async Task<User?> GetUserAsync()
    {
        var id = await GetUserIdAsync();
        if (id is null) return null;
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }
}

/// <summary>"Active" policy: signed in, not blocked, and subscription not expired (admins always pass).</summary>
public class ActiveSubscriptionRequirement : IAuthorizationRequirement;

public class ActiveSubscriptionHandler(IDbContextFactory<AppDbContext> dbFactory)
    : AuthorizationHandler<ActiveSubscriptionRequirement>
{
    public const string Policy = "Active";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveSubscriptionRequirement requirement)
    {
        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return;
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user?.IsActive(DateTime.UtcNow) == true) context.Succeed(requirement);
    }
}
