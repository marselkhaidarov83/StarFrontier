using UnityEngine;

public sealed class DebugSettings : MonoBehaviour
{
    [Header("Debug UI")]
    [Tooltip("Показывает или скрывает отладочный интерфейс при запуске в Unity Editor.")]
    public bool showDebugOverlayInEditor = true;

    [Tooltip("Показывает или скрывает отладочный интерфейс в Android/iOS-сборке.")]
    public bool showDebugOverlayInMobileBuild = false;

    [Header("Mobile Test")]
    [Tooltip("Общий выключатель всех настроек DebugSettings: интерфейса, тестовой новой игры, годмода, NPC и войны. При выключении игра использует обычные конфиги. Сохранения не перезаписываются.")]
    public bool mobileTestMode = false;

    [Tooltip("Размер топливного бака тестового стартового корабля.")]
    [Min(1)] public int mobileTestFuelCapacity = 100;

    [Tooltip("Начальный запас топлива тестового корабля. Ограничивается размером бака.")]
    [Min(0)] public int mobileTestCurrentFuel = 100;

    [Tooltip("Начальное количество кредитов в тестовой новой игре.")]
    [Min(0)] public int mobileTestCredits = 999999;

    [Tooltip("Неуязвимость игрока при включённом Mobile Test Mode.")]
    public bool mobileTestGodMode = true;

    [Tooltip("Сколько первых секторов по Order открыто в тестовой новой игре. Остальные закрыты.")]
    [Min(0)] public int mobileTestUnlockedSectorCount = 5;

    [Tooltip("Сколько первых секторов по Order принадлежат союзникам. Их системы мирные; системы остальных секторов принадлежат вражеским фракциям, указанным в SectorConfig.")]
    [Min(0)] public int mobileTestAlliedSectorCount = 2;

    [Tooltip("Корабль игрока для тестовой новой игры. Если не назначен, используется стартовый корабль из NewGameConfig.")]
    public AllyConfig mobileTestStarterShip;

    public bool IsGodModeEnabled => mobileTestMode && mobileTestGodMode;

    [Header("Debug / NPC Population")]
    [Tooltip("Спавнить NPC только в текущей системе. Существующие NPC в других системах не удаляются.")]
    [SerializeField] private bool debugSpawnOnlyInCurrentSystem;

    [Tooltip("Останавливает автоматический спавн союзных NPC.")]
    [SerializeField] private bool stopAutomaticAllySpawns;

    [Tooltip("Останавливает автоматический спавн врагов.")]
    [SerializeField] private bool stopAutomaticEnemySpawns;

    [Tooltip("Использовать отладочный интервал автоматического спавна NPC и врагов вместо значения из правила.")]
    [SerializeField] private bool overrideAutomaticSpawnInterval;

    [Tooltip("Отладочный интервал автоматического спавна NPC и врагов в секундах.")]
    [SerializeField, Min(0.1f)] private float debugAutomaticSpawnIntervalSeconds = 5f;

    [Tooltip("Использовать отладочный уровень галактики вместо уровня из состояния игры.")]
    [SerializeField] private bool overrideNpcGalaxyLevel;

    [Tooltip("Отладочный уровень галактики для правил спавна NPC и врагов.")]
    [SerializeField, Range(1, 10)] private int debugNpcGalaxyLevel = 1;

    [Header("Debug / War")]
    [Tooltip("Останавливает автоматическое создание новых вторжений. Уже активные вторжения продолжают обновляться.")]
    [SerializeField] private bool stopAutomaticInvasions;

    [Tooltip("Использовать отладочные параметры автоматических вторжений вместо значений из InvasionScheduleConfig.")]
    [SerializeField] private bool overrideAutomaticInvasionSchedule;

    [Tooltip("Отладочный период между попытками создать новое вторжение, в секундах.")]
    [SerializeField, Min(1f)] private float debugAutomaticInvasionPeriodSeconds = 30f;

    [Tooltip("Отладочный максимум активных вторжений одновременно.")]
    [SerializeField, Min(0)] private int debugMaxActiveInvasions = 1;

    public bool DebugSpawnOnlyInCurrentSystem => mobileTestMode && debugSpawnOnlyInCurrentSystem;
    public bool StopAutomaticAllySpawns => mobileTestMode && stopAutomaticAllySpawns;
    public bool StopAutomaticEnemySpawns => mobileTestMode && stopAutomaticEnemySpawns;
    public bool OverrideAutomaticSpawnInterval => mobileTestMode && overrideAutomaticSpawnInterval;
    public bool OverrideNpcGalaxyLevel => mobileTestMode && overrideNpcGalaxyLevel;

    public bool StopAutomaticInvasions => mobileTestMode && stopAutomaticInvasions;
    public bool OverrideAutomaticInvasionSchedule => mobileTestMode && overrideAutomaticInvasionSchedule;

    public float DebugAutomaticSpawnIntervalSeconds =>
        Mathf.Max(0.1f, debugAutomaticSpawnIntervalSeconds);

    public int DebugNpcGalaxyLevel =>
        Mathf.Clamp(debugNpcGalaxyLevel, 1, 10);

    public float DebugAutomaticInvasionPeriodSeconds =>
        Mathf.Max(1f, debugAutomaticInvasionPeriodSeconds);

    public int DebugMaxActiveInvasions =>
        Mathf.Max(0, debugMaxActiveInvasions);

    public bool OverrideAutomaticAllySpawnInterval => OverrideAutomaticSpawnInterval;

    public float DebugAutomaticAllySpawnIntervalSeconds =>
        DebugAutomaticSpawnIntervalSeconds;
}