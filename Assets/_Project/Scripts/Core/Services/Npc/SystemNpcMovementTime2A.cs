using System;

public static class SystemNpcMovementTime2A
{
    public static double Now
    {
        get
        {
            if (Bootstrapper.Instance != null && Bootstrapper.Instance.ServiceRegistry != null &&
                Bootstrapper.Instance.ServiceRegistry.TryGet(out IGameTimeService time))
            {
                return time.SimulationTimeSeconds / (double)GameTimeState.SecondsPerDay;
            }
            return 0d;
        }
    }

    public static bool IsMovingBehavior(SystemNpcBehaviorType behavior)
    {
        return behavior == SystemNpcBehaviorType.PlanetToPlanetTravel ||
            behavior == SystemNpcBehaviorType.TravelToAnotherSystem ||
            behavior == SystemNpcBehaviorType.TravelToEnemySystem ||
            behavior == SystemNpcBehaviorType.PatrolSystem ||
            behavior == SystemNpcBehaviorType.EngageEnemies;
    }

    public static void Reset(SystemNpcRuntimeState npc, double now)
    {
        npc.MovementAccountedThroughTicks = now;
        npc.PendingMovementTicks = 0d;
    }

    public static double Accrue(SystemNpcRuntimeState npc, double now)
    {
        if (npc.MovementAccountedThroughTicks < 0d || now < npc.MovementAccountedThroughTicks)
            Reset(npc, now);

        npc.PendingMovementTicks += Math.Max(0d, now - npc.MovementAccountedThroughTicks);
        npc.MovementAccountedThroughTicks = now;
        return npc.PendingMovementTicks;
    }

    public static void Consume(SystemNpcRuntimeState npc, double usedTicks)
    {
        npc.PendingMovementTicks = Math.Max(0d, npc.PendingMovementTicks - Math.Max(0d, usedTicks));
    }
}
