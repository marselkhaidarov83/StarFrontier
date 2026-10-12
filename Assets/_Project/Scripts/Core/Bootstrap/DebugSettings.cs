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

    public bool DebugSpawnOnlyInCurrentSystem => debugSpawnOnlyInCurrentSystem;
    public bool StopAutomaticAllySpawns => stopAutomaticAllySpawns;
    public bool StopAutomaticEnemySpawns => stopAutomaticEnemySpawns;
    public bool OverrideAutomaticSpawnInterval => overrideAutomaticSpawnInterval;
    public bool OverrideNpcGalaxyLevel => overrideNpcGalaxyLevel;

    public float DebugAutomaticSpawnIntervalSeconds =>
        Mathf.Max(0.1f, debugAutomaticSpawnIntervalSeconds);

    public int DebugNpcGalaxyLevel =>
        Mathf.Clamp(debugNpcGalaxyLevel, 1, 10);

    public bool OverrideAutomaticAllySpawnInterval => OverrideAutomaticSpawnInterval;

    public float DebugAutomaticAllySpawnIntervalSeconds =>
        DebugAutomaticSpawnIntervalSeconds;
}