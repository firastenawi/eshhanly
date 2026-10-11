namespace Eshhanly.Services;

/// <summary>
/// In-process pub/sub so open pages refresh instantly (new message, request answered, ...).
/// Fine for a single server instance; swap for Redis/SignalR backplane if you ever scale out.
/// </summary>
public sealed class LiveUpdates
{
    public event Func<int, Task>? UserChanged;

    public void Notify(params int[] userIds)
    {
        var handlers = UserChanged;
        if (handlers is null) return;
        foreach (var id in userIds.Distinct())
            foreach (Func<int, Task> h in handlers.GetInvocationList())
                _ = SafeInvoke(h, id);
    }

    private static async Task SafeInvoke(Func<int, Task> h, int id)
    {
        try { await h(id); } catch { /* a disconnected circuit must not break others */ }
    }
}
