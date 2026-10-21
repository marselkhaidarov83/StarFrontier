using UnityEngine;

internal static class NpcCombatDistance2A
{
    public static float GetHoldDistance(SystemNpcRuntimeState npc)
    {
        float shortestRange = float.MaxValue;
        if (npc?.Weapons != null)
        {
            for (int i = 0; i < npc.Weapons.Count; i++)
            {
                SystemNpcWeaponRuntimeState weapon = npc.Weapons[i];
                if (weapon != null && weapon.ShotDistance > 0f)
                    shortestRange = Mathf.Min(shortestRange, weapon.ShotDistance);
            }
        }

        // Use the same 10% range reserve as the player's close follow mode.
        return shortestRange < float.MaxValue ? Mathf.Max(3f, shortestRange * 0.9f) : 100f;
    }

    public static float GetArrivalTolerance(SystemNpcRuntimeState npc)
    {
        float turnRadiusPart = Mathf.Max(0f, npc != null ? npc.TurnRadius : 0f) * 0.05f;
        float rangeTolerance = GetHoldDistance(npc) * 0.05f;
        return Mathf.Min(Mathf.Clamp(turnRadiusPart, 3f, 25f), Mathf.Max(3f, rangeTolerance));
    }
}
