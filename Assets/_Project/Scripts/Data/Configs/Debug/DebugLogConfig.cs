using UnityEngine;

public enum DebugLogChannel
{
    Bootstrap,
    ShipRoute,
    PlayerMovement,
    NpcMovement,
    EnemyMovement,
    Combat,
    Damage,
    Population,
    Save,
    Time,
    UI,
    PlanetMovement,
}

public enum NpcMovementLoadAnalyticsDetailLevel
{
    Off,
    SummaryOnly,
    DetailedOnSpike,
    DetailedRegularAndSpike,
    DetailedEveryTick
}

public enum DebugLogPerformanceArea
{
    NpcMovement,
    NpcMovementRouteDecision,
    NpcMovementLoadAnalytics,
    GameTimeLoadAnalytics,
    NpcBehavior,
    EnemyMovement,
    PlayerMovement,
    ShipRoute,
    NpcCombat,
    PlanetMovement,
    GalaxyRoute,
    Save,
    NpcAnnihilation
}

[CreateAssetMenu(
    fileName = "DebugLogConfig",
    menuName = "StarFrontier/Configs/Debug/Debug Log Config")]
public sealed class DebugLogConfig : ScriptableObject
{
    [Header("Master")]
    [SerializeField] private bool logsEnabled = true;

    [Header("Route / Movement")]
    [SerializeField] private bool shipRouteLogs;
    [SerializeField] private bool includeShipRouteInternalLogs;
    [SerializeField] private bool npcRouteDecisionLogs;
    [SerializeField] private bool playerMovementLogs;
    [SerializeField] private bool npcMovementLogs;
    [SerializeField] private bool enemyMovementLogs;

    [SerializeField]
    [Range(0f, 180f)]
    private float movementTurnSpikeAngleDegrees = 120f;

    [Header("Gameplay")]
    [SerializeField] private bool combatLogs;
    [SerializeField] private bool damageLogs;
    [SerializeField] private bool populationLogs;
    [SerializeField] private bool planetMovementLogs;
    [SerializeField] private bool saveLogs;
    [SerializeField] private bool timeLogs;
    [SerializeField] private bool uiLogs;
    [SerializeField] private bool loadingSceneDiagnosticsLogs;
    [SerializeField] private bool waveVisualGeometryLogs;
    [SerializeField] private bool bootstrapLogs;

    [Header("Performance Main")]
    [SerializeField] private bool performanceLogs;
    [SerializeField] private bool disableInfoLogStackTrace = true;

    [Header("Performance Game Time")]
    [SerializeField] private bool gameTimeLoadAnalyticsLogs;
    [SerializeField] private bool gameTimeFullFpsAnalyticsLogs;
    [SerializeField] private int gameTimeFullFpsSampleIntervalFrames = 120;
    [SerializeField] private float gameTimeFrameSpikeWarningMs = 33.34f;
    [SerializeField] private float gameTimeFrameSpikeCriticalMs = 50f;
    [SerializeField] private float gameTimeFrameSpikeRelativeMultiplier = 1.75f;
    [SerializeField] private bool gameTimeSimpleFrameLogs;
    [SerializeField] private int gameTimeSimpleFrameLogIntervalFrames = 1;

    [Header("Performance Movement")]
    [SerializeField] private bool npcMovementPerformanceLogs;
    [SerializeField] private bool npcMovementRouteDecisionPerformanceLogs;
    [SerializeField] private float shipRouteBuildDiagnosticsThresholdMs = 3f;
    [SerializeField] private bool npcMovementLoadAnalyticsLogs;
    [SerializeField] private float npcMovementLoadAnalyticsFpsWarningThreshold = 35f;
    [SerializeField] private float npcMovementLoadAnalyticsFpsCriticalThreshold = 30f;
    [SerializeField] private float npcMovementLoadAnalyticsTickWarningMs = 16.67f;
    [SerializeField] private int npcMovementLoadAnalyticsRegularLogIntervalTicks = 60;
    [SerializeField]
    private NpcMovementLoadAnalyticsDetailLevel npcMovementLoadAnalyticsDetailLevel =
    NpcMovementLoadAnalyticsDetailLevel.SummaryOnly;

    [Header("Performance Behavior")]
    [SerializeField] private bool npcBehaviorActionAnalyticsLogs;
    [SerializeField] private float npcBehaviorActionAnalyticsTickWarningMs = 16.67f;
    [SerializeField] private bool npcBehaviorActionAnalyticsOnFpsWarning = true;
    [SerializeField] private int npcBehaviorActionAnalyticsRegularLogIntervalTicks;

    [SerializeField] private float visualUpdateSpikeThresholdMs = 1f;
    [SerializeField] private bool visualUpdateVerboseLogs;
    [SerializeField] private bool npcBehaviorPerformanceLogs;
    [SerializeField] private bool npcAnnihilationPerformanceLogs;
    [SerializeField] private bool enemyMovementPerformanceLogs;
    [SerializeField] private bool playerMovementPerformanceLogs;
    [SerializeField] private bool shipRoutePerformanceLogs;
    [SerializeField] private bool npcCombatPerformanceLogs;
    [SerializeField] private bool planetMovementPerformanceLogs;
    [SerializeField] private bool galaxyRoutePerformanceLogs;
    [SerializeField] private bool savePerformanceLogs;

    [Header("Noise Controls")]
    [SerializeField] private bool npcTurnSpikeLogs;
    [SerializeField] private bool enemyTurnSpikeLogs;
    [SerializeField] private bool npcMilitaryMovementVerboseLogs;
    [SerializeField] private bool npcPlanetApproachQueueCoordinateLogs;

    public bool LogsEnabled => logsEnabled;

    public bool IncludeShipRouteInternalLogs =>
        logsEnabled && includeShipRouteInternalLogs;

    public bool NpcRouteDecisionLogs =>
        logsEnabled && npcRouteDecisionLogs;

    public float MovementTurnSpikeAngleDegrees =>
        Mathf.Clamp(movementTurnSpikeAngleDegrees, 0f, 180f);

    public bool NpcMovementPerformanceLogs =>
        logsEnabled && npcMovementPerformanceLogs;

    public bool PerformanceLogs =>
        logsEnabled && performanceLogs;

    public bool DisableInfoLogStackTrace =>
        disableInfoLogStackTrace;

    public bool LoadingSceneDiagnosticsLogs =>
        logsEnabled && loadingSceneDiagnosticsLogs;

    public bool NpcTurnSpikeLogs =>
        logsEnabled && npcTurnSpikeLogs;

    public bool EnemyTurnSpikeLogs =>
        logsEnabled && enemyTurnSpikeLogs;

    public NpcMovementLoadAnalyticsDetailLevel NpcMovementLoadAnalyticsDetailLevel =>
npcMovementLoadAnalyticsDetailLevel;

    public bool NpcBehaviorActionAnalyticsLogs =>
        logsEnabled &&
        performanceLogs &&
        npcMovementLoadAnalyticsLogs &&
        npcBehaviorActionAnalyticsLogs &&
        npcMovementLoadAnalyticsDetailLevel != NpcMovementLoadAnalyticsDetailLevel.Off;

    public float NpcBehaviorActionAnalyticsTickWarningMs =>
        Mathf.Max(0f, npcBehaviorActionAnalyticsTickWarningMs);

    public bool NpcBehaviorActionAnalyticsOnFpsWarning =>
        npcBehaviorActionAnalyticsOnFpsWarning;

    public int NpcBehaviorActionAnalyticsRegularLogIntervalTicks =>
        Mathf.Max(0, npcBehaviorActionAnalyticsRegularLogIntervalTicks);

    public bool NpcMovementLoadAnalyticsLogs =>
        logsEnabled && performanceLogs && npcMovementLoadAnalyticsLogs;

    public float NpcMovementLoadAnalyticsFpsWarningThreshold =>
        Mathf.Max(1f, npcMovementLoadAnalyticsFpsWarningThreshold);

    public float NpcMovementLoadAnalyticsFpsCriticalThreshold =>
        Mathf.Max(1f, npcMovementLoadAnalyticsFpsCriticalThreshold);

    public float NpcMovementLoadAnalyticsTickWarningMs =>
        Mathf.Max(0f, npcMovementLoadAnalyticsTickWarningMs);

    public int NpcMovementLoadAnalyticsRegularLogIntervalTicks =>
        Mathf.Max(0, npcMovementLoadAnalyticsRegularLogIntervalTicks);

    public bool GameTimeFullFpsAnalyticsLogs =>
        logsEnabled && performanceLogs && gameTimeLoadAnalyticsLogs && gameTimeFullFpsAnalyticsLogs;

    public bool GameTimeSimpleFrameLogs =>
        logsEnabled && performanceLogs && gameTimeLoadAnalyticsLogs && gameTimeSimpleFrameLogs;

    public int GameTimeSimpleFrameLogIntervalFrames =>
        Mathf.Max(1, gameTimeSimpleFrameLogIntervalFrames);

    public float VisualUpdateSpikeThresholdMs =>
Mathf.Max(0f, visualUpdateSpikeThresholdMs);

    public bool VisualUpdateVerboseLogs =>
        logsEnabled &&
        performanceLogs &&
        gameTimeLoadAnalyticsLogs &&
        visualUpdateVerboseLogs;

    public int GameTimeFullFpsSampleIntervalFrames =>
        Mathf.Max(1, gameTimeFullFpsSampleIntervalFrames);

    public float GameTimeFrameSpikeWarningMs =>
        Mathf.Max(1f, gameTimeFrameSpikeWarningMs);

    public bool NpcMilitaryMovementVerboseLogs =>
        logsEnabled && npcMilitaryMovementVerboseLogs;

    public bool NpcPlanetApproachQueueCoordinateLogs =>
        logsEnabled && npcPlanetApproachQueueCoordinateLogs;

    public float GameTimeFrameSpikeCriticalMs =>
        Mathf.Max(GameTimeFrameSpikeWarningMs, gameTimeFrameSpikeCriticalMs);

    public float GameTimeFrameSpikeRelativeMultiplier =>
        Mathf.Max(1f, gameTimeFrameSpikeRelativeMultiplier);

    public float ShipRouteBuildDiagnosticsThresholdMs =>
        Mathf.Max(0f, shipRouteBuildDiagnosticsThresholdMs);

    public bool WaveVisualGeometryLogs =>
        logsEnabled && waveVisualGeometryLogs;

    public bool IsEnabled(DebugLogChannel channel)
    {
        if (!logsEnabled)
            return false;

        switch (channel)
        {
            case DebugLogChannel.Bootstrap:
                return bootstrapLogs;

            case DebugLogChannel.ShipRoute:
                return shipRouteLogs;

            case DebugLogChannel.PlayerMovement:
                return playerMovementLogs;

            case DebugLogChannel.NpcMovement:
                return npcMovementLogs;

            case DebugLogChannel.EnemyMovement:
                return enemyMovementLogs;

            case DebugLogChannel.Combat:
                return combatLogs;

            case DebugLogChannel.Damage:
                return damageLogs;

            case DebugLogChannel.Population:
                return populationLogs;

            case DebugLogChannel.Save:
                return saveLogs;

            case DebugLogChannel.Time:
                return timeLogs;

            case DebugLogChannel.PlanetMovement:
                return planetMovementLogs;

            case DebugLogChannel.UI:
                return uiLogs;

            default:
                return false;
        }
    }

    public bool IsPerformanceEnabled(DebugLogPerformanceArea area)
    {
        if (!logsEnabled || !performanceLogs)
            return false;

        switch (area)
        {
            case DebugLogPerformanceArea.NpcMovement:
                return npcMovementPerformanceLogs;

            case DebugLogPerformanceArea.NpcMovementRouteDecision:
                return npcMovementRouteDecisionPerformanceLogs;

            case DebugLogPerformanceArea.NpcMovementLoadAnalytics:
                return npcMovementLoadAnalyticsLogs;

            case DebugLogPerformanceArea.GameTimeLoadAnalytics:
                return gameTimeLoadAnalyticsLogs;

            case DebugLogPerformanceArea.NpcBehavior:
                return npcBehaviorPerformanceLogs;

            case DebugLogPerformanceArea.NpcAnnihilation:
                return npcAnnihilationPerformanceLogs;

            case DebugLogPerformanceArea.EnemyMovement:
                return enemyMovementPerformanceLogs;

            case DebugLogPerformanceArea.PlayerMovement:
                return playerMovementPerformanceLogs;

            case DebugLogPerformanceArea.ShipRoute:
                return shipRoutePerformanceLogs;

            case DebugLogPerformanceArea.NpcCombat:
                return npcCombatPerformanceLogs;

            case DebugLogPerformanceArea.PlanetMovement:
                return planetMovementPerformanceLogs;

            case DebugLogPerformanceArea.GalaxyRoute:
                return galaxyRoutePerformanceLogs;

            case DebugLogPerformanceArea.Save:
                return savePerformanceLogs;

            default:
                return false;
        }
    }

    private void OnValidate()
    {
        visualUpdateSpikeThresholdMs =
            Mathf.Max(0f, visualUpdateSpikeThresholdMs);

        movementTurnSpikeAngleDegrees =
            Mathf.Clamp(movementTurnSpikeAngleDegrees, 0f, 180f);

        shipRouteBuildDiagnosticsThresholdMs =
            Mathf.Max(0f, shipRouteBuildDiagnosticsThresholdMs);

        npcMovementLoadAnalyticsFpsWarningThreshold =
            Mathf.Max(1f, npcMovementLoadAnalyticsFpsWarningThreshold);

        npcMovementLoadAnalyticsFpsCriticalThreshold =
            Mathf.Max(1f, npcMovementLoadAnalyticsFpsCriticalThreshold);

        if (npcMovementLoadAnalyticsFpsCriticalThreshold > npcMovementLoadAnalyticsFpsWarningThreshold)
            npcMovementLoadAnalyticsFpsCriticalThreshold = npcMovementLoadAnalyticsFpsWarningThreshold;

        npcMovementLoadAnalyticsTickWarningMs =
            Mathf.Max(0f, npcMovementLoadAnalyticsTickWarningMs);

        npcMovementLoadAnalyticsRegularLogIntervalTicks =
            Mathf.Max(0, npcMovementLoadAnalyticsRegularLogIntervalTicks);

        npcBehaviorActionAnalyticsTickWarningMs =
            Mathf.Max(0f, npcBehaviorActionAnalyticsTickWarningMs);

        npcBehaviorActionAnalyticsRegularLogIntervalTicks =
            Mathf.Max(0, npcBehaviorActionAnalyticsRegularLogIntervalTicks);

        gameTimeFullFpsSampleIntervalFrames =
            Mathf.Max(1, gameTimeFullFpsSampleIntervalFrames);

        gameTimeFrameSpikeWarningMs =
            Mathf.Max(1f, gameTimeFrameSpikeWarningMs);

        gameTimeFrameSpikeCriticalMs =
            Mathf.Max(gameTimeFrameSpikeWarningMs, gameTimeFrameSpikeCriticalMs);

        gameTimeFrameSpikeRelativeMultiplier =
            Mathf.Max(1f, gameTimeFrameSpikeRelativeMultiplier);

        gameTimeSimpleFrameLogIntervalFrames =
            Mathf.Max(1, gameTimeSimpleFrameLogIntervalFrames);
    }
}