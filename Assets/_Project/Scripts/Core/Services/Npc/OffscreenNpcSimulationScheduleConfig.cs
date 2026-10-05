using System;
using UnityEngine;

public enum OffscreenNpcSimulationSystemLimitMode
{
    MaxCount,
    All
}

[Serializable]
public sealed class OffscreenNpcSimulationScheduleTick
{
    [SerializeField]
    [Min(1)]
    private int priority = 1;

    [SerializeField]
    private OffscreenNpcSimulationSystemLimitMode systemLimitMode =
        OffscreenNpcSimulationSystemLimitMode.MaxCount;

    [SerializeField]
    [Range(1, 10)]
    private int maxSystems = 1;

    public OffscreenNpcSimulationScheduleTick()
    {
    }

    public OffscreenNpcSimulationScheduleTick(
        int priority,
        OffscreenNpcSimulationSystemLimitMode systemLimitMode,
        int maxSystems)
    {
        this.priority = Mathf.Max(1, priority);
        this.systemLimitMode = systemLimitMode;
        this.maxSystems = Mathf.Clamp(maxSystems, 1, 10);
    }

    public int Priority => Mathf.Max(1, priority);

    public OffscreenNpcSimulationSystemLimitMode SystemLimitMode =>
        systemLimitMode;

    public bool ProcessAll =>
        systemLimitMode == OffscreenNpcSimulationSystemLimitMode.All;

    public int MaxSystems => Mathf.Clamp(maxSystems, 1, 10);
}

[CreateAssetMenu(
    fileName = "OffscreenNpcSimulationScheduleConfig",
    menuName = "StarFrontier/Configs/Npc/Offscreen NPC Simulation Schedule")]
public sealed class OffscreenNpcSimulationScheduleConfig : ScriptableObject
{
    [SerializeField]
    [Min(1)]
    private int unreachableSystemPriority = 99;

    [SerializeField]
    [Range(1, 500)]
    private int maxOffscreenAssignNewPerSystemTick = 15;

    [SerializeField]
    [Range(1, 500)]
    private int maxOffscreenReassignPerSystemTick = 15;

    [SerializeField]
    [Range(1, 500)]
    private int maxOffscreenActiveBehaviorPerSystemTick = 30;

    [SerializeField]
    [Range(1, 500)]
    private int maxOffscreenMovementNpcsPerSystemTick = 25;

    [SerializeField]
    [Range(1, 500)]
    private int maxOffscreenCompletedTravelsPerSystemTick = 25;

    [SerializeField]
    [Min(0f)]
    private float maxOffscreenCompletedTravelMsPerSystemTick = 11f;

    [SerializeField]
    [Min(0f)]
    private float offscreenCompletedTravelBudgetReserveMs = 1f;

    [SerializeField]
    private OffscreenNpcSimulationScheduleTick[] ticks =
        Array.Empty<OffscreenNpcSimulationScheduleTick>();

    public int UnreachableSystemPriority =>
        Mathf.Max(1, unreachableSystemPriority);

    public int MaxOffscreenAssignNewPerSystemTick =>
        Mathf.Clamp(maxOffscreenAssignNewPerSystemTick, 1, 500);

    public int MaxOffscreenReassignPerSystemTick =>
        Mathf.Clamp(maxOffscreenReassignPerSystemTick, 1, 500);

    public int MaxOffscreenActiveBehaviorPerSystemTick =>
        Mathf.Clamp(maxOffscreenActiveBehaviorPerSystemTick, 1, 500);

    public float MaxOffscreenCompletedTravelMsPerSystemTick =>
        Mathf.Max(0f, maxOffscreenCompletedTravelMsPerSystemTick);

    public int MaxOffscreenCompletedTravelsPerSystemTick =>
        Mathf.Clamp(maxOffscreenCompletedTravelsPerSystemTick, 1, 500);

    public int MaxOffscreenMovementNpcsPerSystemTick =>
        Mathf.Clamp(maxOffscreenMovementNpcsPerSystemTick, 1, 500);

    public float OffscreenCompletedTravelBudgetReserveMs =>
        Mathf.Max(0f, offscreenCompletedTravelBudgetReserveMs);

    public OffscreenNpcSimulationScheduleTick[] Ticks =>
        ticks ?? Array.Empty<OffscreenNpcSimulationScheduleTick>();

    public void ResetToDefault()
    {
        unreachableSystemPriority = 99;
        maxOffscreenAssignNewPerSystemTick = 15;
        maxOffscreenReassignPerSystemTick = 15;
        maxOffscreenActiveBehaviorPerSystemTick = 30;
        maxOffscreenMovementNpcsPerSystemTick = 35;
        maxOffscreenCompletedTravelsPerSystemTick = 25;
        maxOffscreenCompletedTravelMsPerSystemTick = 11f;
        offscreenCompletedTravelBudgetReserveMs = 1f;

        ticks = new[]
        {
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(2, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(3, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(2, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(4, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(2, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(3, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(5, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(2, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(99, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
        new OffscreenNpcSimulationScheduleTick(1, OffscreenNpcSimulationSystemLimitMode.MaxCount, 1),
    };
    }

    private void OnValidate()
    {
        offscreenCompletedTravelBudgetReserveMs =
            Mathf.Max(0f, offscreenCompletedTravelBudgetReserveMs);

        maxOffscreenCompletedTravelMsPerSystemTick =
            Mathf.Max(0f, maxOffscreenCompletedTravelMsPerSystemTick);

        unreachableSystemPriority =
            Mathf.Max(1, unreachableSystemPriority);

        maxOffscreenAssignNewPerSystemTick =
            Mathf.Clamp(maxOffscreenAssignNewPerSystemTick, 1, 500);

        maxOffscreenReassignPerSystemTick =
            Mathf.Clamp(maxOffscreenReassignPerSystemTick, 1, 500);

        maxOffscreenActiveBehaviorPerSystemTick =
            Mathf.Clamp(maxOffscreenActiveBehaviorPerSystemTick, 1, 500);

        maxOffscreenMovementNpcsPerSystemTick =
            Mathf.Clamp(maxOffscreenMovementNpcsPerSystemTick, 1, 500);

        maxOffscreenCompletedTravelsPerSystemTick =
            Mathf.Clamp(maxOffscreenCompletedTravelsPerSystemTick, 1, 500);

        if (ticks == null)
        {
            ticks = Array.Empty<OffscreenNpcSimulationScheduleTick>();
            return;
        }

        for (int i = 0; i < ticks.Length; i++)
        {
            if (ticks[i] == null)
                ticks[i] = new OffscreenNpcSimulationScheduleTick();
        }
    }
}