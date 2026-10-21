using System.Collections.Generic;
using UnityEngine;

public static class MilitaryNpcDiagnostics2A
{
    private static readonly Dictionary<string, int> LastSnapshotTick = new();

    public static bool Enabled(SystemNpcRuntimeState npc)
    {
        return npc != null && npc.IsAlly && npc.AllyRole == AllyRole2A.Military &&
            Bootstrapper.Instance != null && Bootstrapper.Instance.DebugLogConfig != null &&
            Bootstrapper.Instance.DebugLogConfig.NpcMilitaryMovementVerboseLogs &&
            Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.NpcMovement);
    }

    public static int CurrentTick
    {
        get
        {
            if (Bootstrapper.Instance != null && Bootstrapper.Instance.ServiceRegistry != null &&
                Bootstrapper.Instance.ServiceRegistry.TryGet(out IGameTimeService time))
                return time.CurrentQuantTick;
            return -1;
        }
    }

    public static void Log(string stage, SystemNpcRuntimeState npc, int tick, string detail = "")
    {
        if (!Enabled(npc))
            return;

        // Current-system movement runs per frame; sample its snapshots once per game tick.
        // Route decisions and behavior/filter events remain unthrottled.
        if (stage == "MOVEMENT_VISIT" || stage == "MOVEMENT_AFTER" || stage == "MOVEMENT_BLOCKED")
        {
            string key = npc.RuntimeNpcId + "|" + stage;
            if (LastSnapshotTick.TryGetValue(key, out int previousTick) && previousTick == tick)
                return;
            if (LastSnapshotTick.Count > 4096)
                LastSnapshotTick.Clear();
            LastSnapshotTick[key] = tick;
        }

        AppLog.Info(DebugLogChannel.NpcMovement,
            "[MILITARY_NPC_DIAGNOSTIC] Stage=" + stage + " | Tick=" + tick +
            " | RuntimeNpcId=" + npc.RuntimeNpcId + " | ConfigId=" + npc.ConfigId +
            " | SystemId=" + npc.CurrentSystemId + " | Level=" + npc.Level +
            " | IsAlive=" + npc.IsAlive + " | LifeState=" + npc.LifeState +
            " | Scenario=" + npc.CurrentBehaviorScenario +
            " | Behavior=" + npc.CurrentBehavior + " | PrevBehavior=" + npc.PrevBehavior +
            " | HasActiveBehavior=" + npc.HasActiveBehavior +
            " | BehaviorStartedTick=" + npc.BehaviorStartedTick +
            " | BehaviorEndsTick=" + npc.BehaviorEndsTick +
            " | TravelState=" + npc.TravelState + " | IsOnPlanet=" + npc.IsOnPlanet +
            " | CurrentPlanetId=" + npc.CurrentPlanetId + " | TargetPlanetId=" + npc.TargetPlanetId +
            " | TargetSystemId=" + npc.TargetSystemId +
            " | WaitingForRoute=" + npc.IsWaitingForInitialRouteBuild +
            " | ReleaseFromPlanetAfterRoute=" + npc.ReleaseFromPlanetAfterInitialRouteBuild +
            " | InitialRoutePlanetId=" + npc.InitialRouteBuildPlanetId +
            " | TargetNpcId=" + npc.CurrentTargetRuntimeNpcId +
            " | BehaviorTargetNpcId=" + npc.BehaviorTargetRuntimeNpcId +
            " | CombatState=" + npc.CombatState + " | IsFighting=" + npc.IsFighting +
            " | AvailableForCombat=" + npc.IsAvailableForCombat() +
            " | Position=" + npc.CurrentPosition.ToString("F3") +
            " | TargetPosition=" + npc.TargetPosition.ToString("F3") +
            " | PendingMovementTicks=" + npc.PendingMovementTicks.ToString("F3") +
            " | MovementAccountedThrough=" + npc.MovementAccountedThroughTicks.ToString("F3") +
            " | Hull=" + npc.CurrentHull + " | Shield=" + npc.CurrentShield +
            " | " + detail);
    }
}
