using UnityEngine;

public enum DebugLogChannel
{
    ShipRoute,
    PlayerMovement,
    NpcMovement,
    EnemyMovement,
    Combat,
    Damage,
    Population,
    Save,
    Time,
    UI
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
    [SerializeField] private bool saveLogs;
    [SerializeField] private bool timeLogs;
    [SerializeField] private bool uiLogs;

    public bool LogsEnabled => logsEnabled;

    public bool IncludeShipRouteInternalLogs =>
        logsEnabled && includeShipRouteInternalLogs;

    public bool NpcRouteDecisionLogs =>
        logsEnabled && npcRouteDecisionLogs;

    public float MovementTurnSpikeAngleDegrees =>
        Mathf.Clamp(movementTurnSpikeAngleDegrees, 0f, 180f);

    public bool IsEnabled(DebugLogChannel channel)
    {
        if (!logsEnabled)
            return false;

        switch (channel)
        {
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

            case DebugLogChannel.UI:
                return uiLogs;

            default:
                return false;
        }
    }
}