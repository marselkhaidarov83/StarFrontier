using System;

// Optional observation boundary. Missing analytics or an SDK exception never changes a game result.
public static class PlayerAnalyticsSafe
{
    public static IPlayerAnalytics Get()
    {
        try
        {
            var bootstrap = Bootstrapper.Instance;
            if (bootstrap != null && bootstrap.ServiceRegistry != null &&
                bootstrap.ServiceRegistry.TryGet<IPlayerAnalytics>(out var analytics)) return analytics;
        }
        catch { }
        return null;
    }

    public static PlayerAnalyticsOperation Observe(PlayerAnalyticsAction action, string contentId = null, int quantity = 0)
    {
        try { return new PlayerAnalyticsOperation(Get(), action, contentId, quantity); }
        catch { return new PlayerAnalyticsOperation(null, action, null, 0); }
    }

    public static void Track(PlayerAnalyticsAction action, bool success, string reason = "None", string contentId = null)
    {
        try { Get()?.Track(action, success, reason, contentId: contentId); } catch { }
    }
}

