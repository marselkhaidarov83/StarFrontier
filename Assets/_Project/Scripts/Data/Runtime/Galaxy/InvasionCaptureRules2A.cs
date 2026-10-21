using System.Collections.Generic;

public static class InvasionCaptureRules2A
{
    public static bool HasMinimumDurationElapsed(int startedAtTick, int currentTick, int minimumTicks)
    {
        return (long)currentTick - startedAtTick >= System.Math.Max(0, minimumTicks);
    }

    public static bool CanCapture(
        string systemId,
        string playerSystemId,
        IEnumerable<SystemNpcRuntimeState> npcs)
    {
        if (string.IsNullOrWhiteSpace(systemId) ||
            string.IsNullOrWhiteSpace(playerSystemId) ||
            playerSystemId == systemId || npcs == null)
        {
            return false;
        }

        bool hasAliveEnemy = false;
        foreach (SystemNpcRuntimeState npc in npcs)
        {
            if (npc == null || !npc.IsAlive || npc.CurrentSystemId != systemId)
                continue;

            // Military defend ownership on planets too; other allies only defend in space.
            // Match SystemNpcViewBinder's planet check, including the travel-state flag.
            if (npc.NpcType == SystemNpcType.Ally &&
                (npc.AllyRole == AllyRole2A.Military ||
                 (!npc.IsOnPlanet && npc.TravelState != SystemNpcTravelState.OnPlanet)))
                return false;

            if (npc.NpcType == SystemNpcType.Enemy)
                hasAliveEnemy = true;
        }

        return hasAliveEnemy;
    }
}
