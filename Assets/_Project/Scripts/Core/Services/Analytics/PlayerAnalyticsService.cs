using System;
using System.Globalization;
using System.Collections.Generic;

// Pure managed orchestration; the SDK is the only owner of the durable delivery queue.
public sealed class PlayerAnalyticsService : IPlayerAnalytics
{
    private readonly PlayerAnalyticsOptions _options;
    private readonly IPlayerAnalyticsTransport _transport;
    private readonly IPlayerAnalyticsPreferences _preferences;
    private readonly Func<PlayerAnalyticsSnapshot> _capture;
    private readonly Func<DateTime> _utcNow;
    private readonly Action<string> _debugLog;
    private string _sessionId;
    private DateTime? _pausedAt;
    private bool _started;
    private bool _enabled;
    private bool _isFirstSession;
    private bool _firstSessionFinished;
    private DateTime _firstSessionStartedAt;
    private DateTime _previousStageAt;
    private readonly HashSet<string> _reachedStages = new HashSet<string>();
    private readonly List<string> _stagePath = new List<string>();
    private readonly List<string> _stageGaps = new List<string>();
    private PlayerAnalyticsRecord _lastStageRecord;
    private PlayerAnalyticsRecord _sessionStartRecord;
    private string _lastFailedAttempt = "none";
    private string _lastFailureReason = "none";
    private int _firstSessionFailureCount;
    public bool CollectionEnabled => _enabled;
    public string Environment => _options.IsTest ? "test" : "production";
    public int AcceptedEvents { get; private set; }
    public int RejectedEvents { get; private set; }

    public PlayerAnalyticsService(PlayerAnalyticsOptions options, IPlayerAnalyticsTransport transport,
        IPlayerAnalyticsPreferences preferences, Func<PlayerAnalyticsSnapshot> capture,
        Func<DateTime> utcNow = null, Action<string> debugLog = null)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Copy();
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _debugLog = debugLog;
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
        _isFirstSession = _preferences.FirstSessionPending;
        _firstSessionFinished = false;
        _firstSessionStartedAt = _utcNow();
        _previousStageAt = _firstSessionStartedAt;
        _reachedStages.Clear();
        _stagePath.Clear();
        _stageGaps.Clear();
        _lastStageRecord = null;
        _sessionStartRecord = null;
        _lastFailedAttempt = "none";
        _lastFailureReason = "none";
        _firstSessionFailureCount = 0;
        Track(PlayerAnalyticsAction.SessionStarted, true);
        if (_isFirstSession && _sessionStartRecord != null)
            _preferences.FirstSessionPending = false;
    }

    public void Track(PlayerAnalyticsAction action, bool success, string reason = "None",
        PlayerAnalyticsSnapshot before = null, string contentId = null, int quantity = 0, int cost = 0)
    {
        if (!_enabled || !_started) return;
        try
        {
            if (!Enum.IsDefined(typeof(PlayerAnalyticsAction), action)) { RejectedEvents++; return; }
            if (_isFirstSession && !success)
            {
                _lastFailedAttempt = EventName(action);
                _lastFailureReason = SafeCode(reason);
                _firstSessionFailureCount++;
            }
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
                fuel_before = before.Fuel, fuel_after = after.Fuel, quantity = quantity, cost = cost,
                is_first_session = _isFirstSession,
                first_session_elapsed_seconds = _isFirstSession
                    ? SecondsBetween(_firstSessionStartedAt, _utcNow()) : 0,
                first_session_stage_path = _isFirstSession ? string.Join(">", _stagePath) : "none",
                first_session_stage_gaps = _isFirstSession ? string.Join(",", _stageGaps) : "none",
                last_first_session_stage = _isFirstSession && _stagePath.Count > 0
                    ? _stagePath[_stagePath.Count - 1] : "none",
                first_session_last_attempt = _isFirstSession ? _lastFailedAttempt : "none",
                first_session_last_failure_reason = _isFirstSession ? _lastFailureReason : "none",
                first_session_failure_count = _isFirstSession ? _firstSessionFailureCount : 0
            };
            if (!Guid.TryParse(record.installation_id, out _)) { RejectedEvents++; return; }
            _transport.Report(record);
            AcceptedEvents++;
            if (_isFirstSession && !success)
                LogFirstSession("BLOCKER Action=" + record.action + " Reason=" + record.reason +
                    " LastStage=" + record.last_first_session_stage);
            if (action == PlayerAnalyticsAction.SessionStarted) _sessionStartRecord = record;
            if (success) TryTrackFirstSessionStage(action, record);
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
            {
                FinishSession();
                StartSession();
            }
            if (!paused) _pausedAt = null;
            if (_started && _enabled) { _transport.Pause(paused); if (paused) _transport.Flush(); }
        }
        catch { RejectedEvents++; }
    }

    public void FinishSession()
    {
        if (!_enabled || !_started || !_isFirstSession || _firstSessionFinished) return;
        if (_sessionStartRecord == null) return;
        try
        {
            var report = (_lastStageRecord ?? _sessionStartRecord).Copy();
            report.event_id = Guid.NewGuid().ToString("N");
            report.action = EventName(PlayerAnalyticsAction.FirstSessionReport);
            report.occurred_at_utc = _utcNow().ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            report.first_session_elapsed_seconds = SecondsBetween(_firstSessionStartedAt, _utcNow());
            report.first_session_step_seconds = 0;
            report.first_session_last_attempt = _lastFailedAttempt;
            report.first_session_last_failure_reason = _lastFailureReason;
            report.first_session_failure_count = _firstSessionFailureCount;
            report.last_first_session_stage = _lastStageRecord?.first_session_stage ?? "none";
            _transport.Report(report);
            AcceptedEvents++;
            _firstSessionFinished = true;
            LogFirstSession("REPORT Path=" + report.first_session_stage_path +
                " Gaps=" + report.first_session_stage_gaps +
                " LastStage=" + report.last_first_session_stage);
        }
        catch { RejectedEvents++; }
    }

    private void TryTrackFirstSessionStage(PlayerAnalyticsAction action, PlayerAnalyticsRecord source)
    {
        if (!_isFirstSession || _firstSessionFinished) return;
        string stage = FirstSessionStage(action);
        if (stage == null || !_reachedStages.Add(stage)) return;
        DateTime previous = _previousStageAt;
        try
        {
            DateTime now = _utcNow();
            int stepSeconds = SecondsBetween(_previousStageAt, now);
            _previousStageAt = now;
            _stagePath.Add(stage);
            _stageGaps.Add(stepSeconds.ToString(CultureInfo.InvariantCulture));
            var milestone = source.Copy();
            milestone.event_id = Guid.NewGuid().ToString("N");
            milestone.action = EventName(PlayerAnalyticsAction.FirstSessionStage);
            milestone.occurred_at_utc = now.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            milestone.first_session_stage = stage;
            milestone.first_session_stage_index = _stagePath.Count;
            milestone.first_session_elapsed_seconds = SecondsBetween(_firstSessionStartedAt, now);
            milestone.first_session_step_seconds = stepSeconds;
            milestone.first_session_stage_path = string.Join(">", _stagePath);
            milestone.first_session_stage_gaps = string.Join(",", _stageGaps);
            milestone.last_first_session_stage = stage;
            _transport.Report(milestone);
            _lastStageRecord = milestone;
            AcceptedEvents++;
            LogFirstSession("STAGE " + stage + " Index=" + milestone.first_session_stage_index +
                " StepSeconds=" + stepSeconds);
        }
        catch
        {
            _reachedStages.Remove(stage);
            if (_stagePath.Count > 0 && _stagePath[_stagePath.Count - 1] == stage)
            {
                _stagePath.RemoveAt(_stagePath.Count - 1);
                _stageGaps.RemoveAt(_stageGaps.Count - 1);
            }
            _previousStageAt = previous;
            RejectedEvents++;
        }
    }

    private static int SecondsBetween(DateTime start, DateTime end)
        => (int)Math.Min(int.MaxValue, Math.Max(0d, (end - start).TotalSeconds));

    private void LogFirstSession(string message)
    {
        try { _debugLog?.Invoke("[SF_FIRST_SESSION] " + message); } catch { }
    }

    private static string FirstSessionStage(PlayerAnalyticsAction action)
    {
        switch (action)
        {
            case PlayerAnalyticsAction.FirstLaunch: return "launch";
            case PlayerAnalyticsAction.Travel: return "travel";
            case PlayerAnalyticsAction.CombatStarted: return "combat";
            case PlayerAnalyticsAction.MissionAccept: return "mission_accept";
            case PlayerAnalyticsAction.MissionComplete: return "mission_complete";
            case PlayerAnalyticsAction.LootCollect: return "loot";
            case PlayerAnalyticsAction.ShipUpgrade: return "upgrade";
            default: return null;
        }
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
            case PlayerAnalyticsAction.FirstLaunch: return "sf_first_launch";
            case PlayerAnalyticsAction.LootCollect: return "sf_loot_collect";
            case PlayerAnalyticsAction.ShipUpgrade: return "sf_ship_upgrade";
            case PlayerAnalyticsAction.TargetSelection: return "sf_target_selection";
            case PlayerAnalyticsAction.CargoOverload: return "sf_cargo_overload";
            case PlayerAnalyticsAction.FirstSessionStage: return "sf_first_session_stage";
            case PlayerAnalyticsAction.FirstSessionReport: return "sf_first_session_report";
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
