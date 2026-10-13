using UnityEngine;

public sealed class DebugSettings : MonoBehaviour
{
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

    public bool DebugSpawnOnlyInCurrentSystem => debugSpawnOnlyInCurrentSystem;
    public bool StopAutomaticAllySpawns => stopAutomaticAllySpawns;
    public bool StopAutomaticEnemySpawns => stopAutomaticEnemySpawns;
    public bool OverrideAutomaticSpawnInterval => overrideAutomaticSpawnInterval;
    public bool OverrideNpcGalaxyLevel => overrideNpcGalaxyLevel;

    public bool StopAutomaticInvasions => stopAutomaticInvasions;
    public bool OverrideAutomaticInvasionSchedule => overrideAutomaticInvasionSchedule;

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