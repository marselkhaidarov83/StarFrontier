using System;
using System.Globalization;

// Pure managed orchestration; the SDK is the only owner of the durable delivery queue.
public sealed class PlayerAnalyticsService : IPlayerAnalytics
{
    private readonly PlayerAnalyticsOptions _options;
    private readonly IPlayerAnalyticsTransport _transport;
    private readonly IPlayerAnalyticsPreferences _preferences;
    private readonly Func<PlayerAnalyticsSnapshot> _capture;
    private readonly Func<DateTime> _utcNow;
    private string _sessionId;
    private DateTime? _pausedAt;
    private bool _started;
    private bool _enabled;
    public bool CollectionEnabled => _enabled;
    public string Environment => _options.IsTest ? "test" : "production";
    public int AcceptedEvents { get; private set; }
    public int RejectedEvents { get; private set; }

    public PlayerAnalyticsService(PlayerAnalyticsOptions options, IPlayerAnalyticsTransport transport,
        IPlayerAnalyticsPreferences preferences, Func<PlayerAnalyticsSnapshot> capture, Func<DateTime> utcNow = null)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Copy();
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        SetCollectionEnabled(preferences.CollectionEnabled);
    }

    public PlayerAnalyticsSnapshot Capture()
    {
        try { return _capture() ?? new PlayerAnalyticsSnapshot(); }
        catch { return new PlayerAnalyticsSnapshot(); }
    }

    public void SetCollectionEnabled(bool enabled)
    {
        // Saving a setting and invoking an external SDK must never break gameplay.
        try { _preferences.CollectionEnabled = enabled; } catch { }
        _enabled = enabled && _options.Enabled;
        try
        {
            if (!_enabled)
            {
                if (_started) { _transport.SetEnabled(false); _transport.Pause(true); }
                _sessionId = null;
                return;
            }
            if (!_started)
            {
                string key = _options.IsTest ? _options.TestApiKey : _options.ProductionApiKey;
                if (!Guid.TryParse(key, out _) || !_transport.Start(_options, key))
                { _enabled = false; RejectedEvents++; return; }
                _started = true;
            }
            _transport.SetEnabled(true);
            _transport.Pause(false);
            if (_sessionId == null) StartSession();
        }
        catch { _enabled = false; RejectedEvents++; }
    }

    private void StartSession()
    {
        _sessionId = Guid.NewGuid().ToString("N");
        Track(PlayerAnalyticsAction.SessionStarted, true);
    }

    public void Track(PlayerAnalyticsAction action, bool success, string reason = "None",
        PlayerAnalyticsSnapshot before = null, string contentId = null, int quantity = 0, int cost = 0)
    {
        if (!_enabled || !_started) return;
        try
        {
            if (!Enum.IsDefined(typeof(PlayerAnalyticsAction), action)) { RejectedEvents++; return; }
            PlayerAnalyticsSnapshot after = Capture();
            before = before ?? after;
            var record = new PlayerAnalyticsRecord
            {
                event_id = Guid.NewGuid().ToString("N"),
                installation_id = _preferences.InstallationId,
                session_id = _sessionId,
                occurred_at_utc = _utcNow().ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                app_version = SafeCode(_options.AppVersion), build_number = SafeCode(_options.BuildNumber),
                balance_version = SafeCode(_options.BalanceVersion), environment = Environment,
                platform = SafeCode(_options.Platform), action = EventName(action),
                result = success ? "success" : "failure", reason = success ? "None" : SafeCode(reason),
                content_id = SafeCode(contentId), system_id = SafeCode(after.SystemId),
                planet_id = SafeCode(after.PlanetId), progression_sector_count = after.SectorCount,
                player_level = after.PlayerLevel, has_game = after.HasGame,
                credits_before = before.Credits, credits_after = after.Credits,
                credits_delta = after.Credits - before.Credits,
                diamonds_before = before.Diamonds, diamonds_after = after.Diamonds,
                fuel_before = before.Fuel, fuel_after = after.Fuel, quantity = quantity, cost = cost
            };
            if (!Guid.TryParse(record.installation_id, out _)) { RejectedEvents++; return; }
            _transport.Report(record);
            AcceptedEvents++;
        }
        catch { RejectedEvents++; }
    }

    public void OnApplicationPause(bool paused)
    {
        try
        {
            if (paused) _pausedAt = _utcNow();
            else if (_enabled && _pausedAt.HasValue &&
                (_utcNow() - _pausedAt.Value).TotalSeconds >= Math.Max(10, _options.SessionTimeoutSeconds))
                StartSession();
            if (!paused) _pausedAt = null;
            if (_started && _enabled) { _transport.Pause(paused); if (paused) _transport.Flush(); }
        }
        catch { RejectedEvents++; }
    }

    public void Flush()
    {
        if (!_enabled || !_started) return;
        try { _transport.Flush(); } catch { RejectedEvents++; }
    }

    public static string EventName(PlayerAnalyticsAction action)
    {
        switch (action)
        {
            case PlayerAnalyticsAction.SessionStarted: return "sf_session_started";
            case PlayerAnalyticsAction.NewGame: return "sf_new_game";
            case PlayerAnalyticsAction.ContinueGame: return "sf_continue_game";
            case PlayerAnalyticsAction.Travel: return "sf_travel_result";
            case PlayerAnalyticsAction.Interaction: return "sf_interaction_result";
            case PlayerAnalyticsAction.BuyItem: return "sf_buy_item";
            case PlayerAnalyticsAction.SellItem: return "sf_sell_item";
            case PlayerAnalyticsAction.Refuel: return "sf_refuel";
            case PlayerAnalyticsAction.Repair: return "sf_repair";
            case PlayerAnalyticsAction.MissionAccept: return "sf_mission_accept";
            case PlayerAnalyticsAction.MissionComplete: return "sf_mission_complete";
            case PlayerAnalyticsAction.MissionFail: return "sf_mission_fail";
            case PlayerAnalyticsAction.MissionReward: return "sf_mission_reward";
            case PlayerAnalyticsAction.CombatStarted: return "sf_combat_started";
            case PlayerAnalyticsAction.CombatVictory: return "sf_combat_victory";
            case PlayerAnalyticsAction.CombatDefeat: return "sf_combat_defeat";
            case PlayerAnalyticsAction.PlayerDestroyed: return "sf_player_destroyed";
            case PlayerAnalyticsAction.GovernmentReward: return "sf_government_reward";
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    // Only code/config identifiers, never display names, messages, URLs or free text.
    public static string SafeCode(string value)
    {
        if (string.IsNullOrEmpty(value)) return "none";
        if (value.Length > 96) return "invalid_code";
        foreach (char c in value)
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.')) return "invalid_code";
        return value;
    }
}
