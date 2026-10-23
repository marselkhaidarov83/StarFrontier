using UnityEngine;

public static class PlayerAnalyticsUnityFactory
{
    public static IPlayerAnalytics Create(PlayerAnalyticsConfig config, DebugLogConfig debugAsset,
        DebugSettings debugSettings, IGameSessionService session)
    {
        var options = new PlayerAnalyticsOptions
        {
            Enabled = config.collectionEnabled,
            IsTest = PlayerAnalyticsOptions.UseTest(Application.isEditor, Debug.isDebugBuild,
                debugSettings != null && debugSettings.mobileTestMode,
                debugAsset != null && debugAsset.TestPlayerAnalytics),
            ProductionApiKey = config.productionApiKey, TestApiKey = config.testApiKey,
            AppVersion = Application.version, BuildNumber = BuildNumber(),
            BalanceVersion = config.balanceVersion, Platform = Application.platform.ToString(),
            MaxQueuedReports = config.maxQueuedReports,
            DispatchPeriodSeconds = config.dispatchPeriodSeconds,
            SessionTimeoutSeconds = config.sessionTimeoutSeconds
        };
        return new PlayerAnalyticsService(options, new AppMetricaPlayerAnalyticsTransport(),
            new UnityPlayerAnalyticsPreferences(), () => Capture(session));
    }

    private static string BuildNumber()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
            using (var info = manager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0))
                return info.Get<int>("versionCode").ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { return "unavailable"; }
#else
        return "editor";
#endif
    }

    public static PlayerAnalyticsSnapshot Capture(IGameSessionService session)
    {
        var state = session?.State;
        var player = state?.Player;
        var ship = player?.PlayerShipState?.GetActiveShip();
        var result = new PlayerAnalyticsSnapshot
        {
            HasGame = player != null, SystemId = player?.CurrentSystemId,
            PlanetId = player?.CurrentPlanetId, PlayerLevel = player?.Level ?? 0,
            Credits = player?.Credits ?? 0, Diamonds = player?.Diamonds ?? 0,
            Fuel = ship?.CurrentFuel ?? 0
        };
        if (state?.Galaxy?.Sectors != null)
            foreach (var sector in state.Galaxy.Sectors)
                if (sector != null && sector.IsUnlocked) result.SectorCount++;
        return result;
    }
}
