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

// Конфиг управляет служебными сообщениями и замерами скорости работы:
// какие разделы выводить, насколько подробно и при каких порогах нагрузки.
[CreateAssetMenu(
    fileName = "DebugLogConfig",
    menuName = "StarFrontier/Configs/Debug/Debug Log Config")]
public sealed class DebugLogConfig : ScriptableObject
{
    [Header("Master")]
    [Tooltip("Главный выключатель всех служебных сообщений. Если он выключен, остальные переключатели сообщений не сработают.")]
    [SerializeField] private bool logsEnabled = true;

    [Header("Route / Movement")]
    [Tooltip("Включает сообщения о маршрутах корабля. Используется при выводе информации о построении и прохождении пути.")]
    [SerializeField] private bool shipRouteLogs;

    [Tooltip("Добавляет подробные внутренние сообщения о маршрутах корабля. Используется только вместе с сообщениями маршрутов.")]
    [SerializeField] private bool includeShipRouteInternalLogs;

    [Tooltip("Включает сообщения о выборе маршрута неигровыми кораблями. Используется при разборе решений их перемещения.")]
    [SerializeField] private bool npcRouteDecisionLogs;

    [Tooltip("Включает сообщения о движении игрока. Используется системами перемещения и путешествия.")]
    [SerializeField] private bool playerMovementLogs;

    [Tooltip("Включает сообщения о движении неигровых кораблей. Используется системами их перемещения.")]
    [SerializeField] private bool npcMovementLogs;

    [Tooltip("Включает сообщения о движении врагов. Используется системой движения вражеских кораблей.")]
    [SerializeField] private bool enemyMovementLogs;

    [Tooltip("Угол резкого поворота, после которого движение считается подозрительным. Используется при диагностике поворотов врагов и неигровых кораблей.")]
    [SerializeField]
    [Range(0f, 180f)]
    private float movementTurnSpikeAngleDegrees = 120f;

    [Header("Gameplay")]
    [Tooltip("Включает сообщения о боевых действиях. Используется боевыми системами и отладочными обработчиками.")]
    [SerializeField] private bool combatLogs;

    [Tooltip("Включает сообщения об уроне. Используется при проверке выбора цели, попаданий и расчёта прочности.")]
    [SerializeField] private bool damageLogs;

    [Tooltip("Включает сообщения о наполнении системы кораблями. Используется системами создания и обновления населения системы.")]
    [SerializeField] private bool populationLogs;

    [Tooltip("Включает сообщения о движении планет. Используется орбитальными и визуальными частями карты системы.")]
    [SerializeField] private bool planetMovementLogs;

    [Tooltip("Включает сообщения о сохранении. Используется при диагностике записи и чтения состояния игры.")]
    [SerializeField] private bool saveLogs;

    [Tooltip("Включает сообщения об игровом времени. Используется системой времени при паузе и продвижении дней.")]
    [SerializeField] private bool timeLogs;

    [Tooltip("Включает сообщения интерфейса. Используется экранами и панелями, где добавлена отладочная печать.")]
    [SerializeField] private bool uiLogs;

    [Tooltip("Включает сообщения загрузочного экрана. Используется при диагностике перехода между сценами.")]
    [SerializeField] private bool loadingSceneDiagnosticsLogs;

    [Tooltip("Включает сообщения о геометрии волнового оружия. Используется видом волны для проверки формы и положения эффекта.")]
    [SerializeField] private bool waveVisualGeometryLogs;

    [Tooltip("Включает сообщения начальной загрузки игры. Используется главным загрузчиком при создании систем.")]
    [SerializeField] private bool bootstrapLogs;

    [Header("Performance Main")]
    [Tooltip("Главный выключатель сообщений о скорости работы. Если он выключен, отдельные разделы замеров не выводятся.")]
    [SerializeField] private bool performanceLogs;

    [Tooltip("Отключает подробный след вызовов у обычных сообщений. Используется, чтобы служебные сообщения меньше засоряли окно вывода.")]
    [SerializeField] private bool disableInfoLogStackTrace = true;

    [Header("Performance Game Time")]
    [Tooltip("Включает замеры нагрузки игрового времени. Используется системой времени и общими проверками скорости кадра.")]
    [SerializeField] private bool gameTimeLoadAnalyticsLogs;

    [Tooltip("Включает расширенные замеры частоты кадров. Используется вместе с замерами игрового времени.")]
    [SerializeField] private bool gameTimeFullFpsAnalyticsLogs;

    [Tooltip("Через сколько кадров собирать расширенную сводку частоты кадров. Используется при анализе нагрузки игрового времени.")]
    [SerializeField] private int gameTimeFullFpsSampleIntervalFrames = 120;

    [Tooltip("Порог предупреждения для долгого кадра в миллисекундах. Используется при поиске просадок скорости.")]
    [SerializeField] private float gameTimeFrameSpikeWarningMs = 33.34f;

    [Tooltip("Критический порог долгого кадра в миллисекундах. Используется для выделения самых тяжёлых просадок.")]
    [SerializeField] private float gameTimeFrameSpikeCriticalMs = 50f;

    [Tooltip("Во сколько раз кадр должен быть тяжелее обычного, чтобы считаться скачком нагрузки. Используется в анализе скорости кадра.")]
    [SerializeField] private float gameTimeFrameSpikeRelativeMultiplier = 1.75f;

    [Tooltip("Включает простые сообщения по каждому выбранному кадру. Используется для быстрой проверки частоты кадров без подробной сводки.")]
    [SerializeField] private bool gameTimeSimpleFrameLogs;

    [Tooltip("Через сколько кадров выводить простые сообщения о скорости. Используется только при включённых простых сообщениях.")]
    [SerializeField] private int gameTimeSimpleFrameLogIntervalFrames = 1;

    [Header("Performance Movement")]
    [Tooltip("Включает замеры скорости движения неигровых кораблей. Используется в общей системе сообщений о скорости.")]
    [SerializeField] private bool npcMovementPerformanceLogs;

    [Tooltip("Включает замеры выбора маршрута неигровыми кораблями. Используется при поиске тяжёлых решений маршрута.")]
    [SerializeField] private bool npcMovementRouteDecisionPerformanceLogs;

    [Tooltip("Порог времени построения маршрута корабля. Если построение дольше, выводится диагностическое сообщение.")]
    [SerializeField] private float shipRouteBuildDiagnosticsThresholdMs = 3f;

    [Tooltip("Включает сводку нагрузки движения неигровых кораблей. Используется системами движения для поиска перегрузок.")]
    [SerializeField] private bool npcMovementLoadAnalyticsLogs;

    [Tooltip("Порог частоты кадров для предупреждения о нагрузке движения. Используется в сводке движения неигровых кораблей.")]
    [SerializeField] private float npcMovementLoadAnalyticsFpsWarningThreshold = 35f;

    [Tooltip("Критический порог частоты кадров для нагрузки движения. Используется в сводке движения неигровых кораблей.")]
    [SerializeField] private float npcMovementLoadAnalyticsFpsCriticalThreshold = 30f;

    [Tooltip("Порог долгого шага движения неигровых кораблей в миллисекундах. Используется для предупреждений о тяжёлом обновлении.")]
    [SerializeField] private float npcMovementLoadAnalyticsTickWarningMs = 16.67f;

    [Tooltip("Через сколько шагов выводить регулярную сводку движения. Ноль оставляет только сообщения по событиям.")]
    [SerializeField] private int npcMovementLoadAnalyticsRegularLogIntervalTicks = 60;

    [Tooltip("Уровень подробности сводки движения неигровых кораблей. Используется при выборе короткого или подробного вывода.")]
    [SerializeField]
    private NpcMovementLoadAnalyticsDetailLevel npcMovementLoadAnalyticsDetailLevel =
    NpcMovementLoadAnalyticsDetailLevel.SummaryOnly;

    [Header("Performance Behavior")]
    [Tooltip("Включает замеры действий поведения неигровых кораблей. Используется при поиске тяжёлых решений поведения.")]
    [SerializeField] private bool npcBehaviorActionAnalyticsLogs;

    [Tooltip("Порог долгого шага поведения в миллисекундах. Используется для предупреждений о тяжёлом поведении кораблей.")]
    [SerializeField] private float npcBehaviorActionAnalyticsTickWarningMs = 16.67f;

    [Tooltip("Разрешает вывод подробностей поведения при падении частоты кадров. Используется вместе со сводкой нагрузки движения.")]
    [SerializeField] private bool npcBehaviorActionAnalyticsOnFpsWarning = true;

    [Tooltip("Через сколько шагов выводить регулярную сводку поведения. Ноль отключает регулярный вывод.")]
    [SerializeField] private int npcBehaviorActionAnalyticsRegularLogIntervalTicks;

    [Tooltip("Порог долгого обновления видимой части кадра в миллисекундах. Используется визуальными контроллерами и панелями.")]
    [SerializeField] private float visualUpdateSpikeThresholdMs = 1f;

    [Tooltip("Включает подробные сообщения о визуальных обновлениях. Используется общей системой замеров видимой части кадра.")]
    [SerializeField] private bool visualUpdateVerboseLogs;

    [Tooltip("Включает замеры скорости поведения неигровых кораблей. Используется в общей системе сообщений о скорости.")]
    [SerializeField] private bool npcBehaviorPerformanceLogs;

    [Tooltip("Включает замеры массового уничтожения неигровых кораблей. Используется при проверке тяжёлых боевых ситуаций.")]
    [SerializeField] private bool npcAnnihilationPerformanceLogs;

    [Tooltip("Включает замеры скорости движения врагов. Используется в общей системе сообщений о скорости.")]
    [SerializeField] private bool enemyMovementPerformanceLogs;

    [Tooltip("Включает замеры скорости движения игрока. Используется в общей системе сообщений о скорости.")]
    [SerializeField] private bool playerMovementPerformanceLogs;

    [Tooltip("Включает замеры скорости построения маршрутов корабля. Используется при диагностике перемещения по системе.")]
    [SerializeField] private bool shipRoutePerformanceLogs;

    [Tooltip("Включает замеры скорости боя неигровых кораблей. Используется при проверке нагрузки от выстрелов и попаданий.")]
    [SerializeField] private bool npcCombatPerformanceLogs;

    [Tooltip("Включает замеры скорости движения планет. Используется в общей системе сообщений о скорости.")]
    [SerializeField] private bool planetMovementPerformanceLogs;

    [Tooltip("Включает замеры скорости маршрутов по галактике. Используется в общей системе сообщений о скорости.")]
    [SerializeField] private bool galaxyRoutePerformanceLogs;

    [Tooltip("Включает замеры скорости сохранения. Используется в общей системе сообщений о скорости.")]
    [SerializeField] private bool savePerformanceLogs;

    [Header("Noise Controls")]
    [Tooltip("Включает сообщения о резких поворотах неигровых кораблей. Используется при диагностике движения.")]
    [SerializeField] private bool npcTurnSpikeLogs;

    [Tooltip("Включает сообщения о резких поворотах врагов. Используется при диагностике движения врагов.")]
    [SerializeField] private bool enemyTurnSpikeLogs;

    [Tooltip("Включает подробные сообщения военного движения неигровых кораблей. Используется для проверки боевых перемещений.")]
    [SerializeField] private bool npcMilitaryMovementVerboseLogs;

    [Tooltip("Включает сообщения о координатах очереди подхода к планете. Используется при проверке движения к планетам.")]
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