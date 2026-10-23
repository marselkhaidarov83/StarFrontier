using System;

public enum PlayerAnalyticsAction
{
    SessionStarted, NewGame, ContinueGame, Travel, Interaction, BuyItem, SellItem,
    Refuel, Repair, MissionAccept, MissionComplete, MissionFail, MissionReward,
    CombatStarted, CombatVictory, CombatDefeat, PlayerDestroyed, GovernmentReward
}

// Deliberately flat and closed: never serialize PlayerState, messages, names or SDK identifiers.
[Serializable]
public sealed class PlayerAnalyticsRecord
{
    public int schema_version = 1;
    public string event_id;
    public string installation_id;
    public string session_id;
    public string occurred_at_utc;
    public string app_version;
    public string build_number;
    public string balance_version;
    public string environment;
    public string platform;
    public string action;
    public string result;
    public string reason;
    public string content_id;
    public string system_id;
    public string planet_id;
    public int progression_sector_count;
    public int player_level;
    public bool has_game;
    public int credits_before;
    public int credits_after;
    public int credits_delta;
    public int diamonds_before;
    public int diamonds_after;
    public int fuel_before;
    public int fuel_after;
    public int quantity;
    public int cost;
}

public sealed class PlayerAnalyticsSnapshot
{
    public bool HasGame;
    public string SystemId;
    public string PlanetId;
    public int SectorCount;
    public int PlayerLevel;
    public int Credits;
    public int Diamonds;
    public int Fuel;
}

public sealed class PlayerAnalyticsOptions
{
    public bool Enabled = true;
    public bool IsTest;
    public string ProductionApiKey;
    public string TestApiKey;
    public string AppVersion;
    public string BuildNumber;
    public string BalanceVersion;
    public string Platform;
    public int MaxQueuedReports = 1000;
    public int DispatchPeriodSeconds = 30;
    public int SessionTimeoutSeconds = 1800;

    public PlayerAnalyticsOptions Copy() => (PlayerAnalyticsOptions)MemberwiseClone();

    public static bool UseTest(bool editor, bool development, bool mobileTest, bool debugAssetTest)
        => editor || development || mobileTest || debugAssetTest;
}

public interface IPlayerAnalyticsTransport
{
    bool Start(PlayerAnalyticsOptions options, string apiKey);
    void Report(PlayerAnalyticsRecord record);
    void SetEnabled(bool enabled);
    void Pause(bool paused);
    void Flush();
}

public interface IPlayerAnalyticsPreferences
{
    string InstallationId { get; }
    bool CollectionEnabled { get; set; }
}

public interface IPlayerAnalytics
{
    bool CollectionEnabled { get; }
    string Environment { get; }
    int AcceptedEvents { get; }
    int RejectedEvents { get; }
    PlayerAnalyticsSnapshot Capture();
    void Track(PlayerAnalyticsAction action, bool success, string reason = "None",
        PlayerAnalyticsSnapshot before = null, string contentId = null, int quantity = 0, int cost = 0);
    void SetCollectionEnabled(bool enabled);
    void OnApplicationPause(bool paused);
    void Flush();
}
