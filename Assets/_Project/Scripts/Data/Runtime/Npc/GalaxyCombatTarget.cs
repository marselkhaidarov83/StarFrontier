using UnityEngine;

public readonly struct GalaxyCombatTarget
{
    public readonly CombatTargetType TargetType;
    public readonly string TargetNpcId;
    public readonly Vector3 Position;
    public readonly bool IsValid;

    public bool IsPlayer => TargetType == CombatTargetType.Player;
    public bool IsNpc => TargetType == CombatTargetType.Npc;

    private GalaxyCombatTarget(
        CombatTargetType targetType,
        string targetNpcId,
        Vector3 position,
        bool isValid)
    {
        TargetType = targetType;
        TargetNpcId = targetNpcId;
        Position = position;
        IsValid = isValid;
    }

    public static GalaxyCombatTarget None()
    {
        return new GalaxyCombatTarget(
            CombatTargetType.None,
            string.Empty,
            Vector3.zero,
            false
        );
    }

    public static GalaxyCombatTarget Player(Vector3 position)
    {
        return new GalaxyCombatTarget(
            CombatTargetType.Player,
            "PLAYER",
            position,
            true
        );
    }

    // TargetNpcId is the generic target key for existing weapon/FX payloads; Station carries the station config ID.
    public static GalaxyCombatTarget Station(string stationId, Vector3 position) =>
        new GalaxyCombatTarget(CombatTargetType.Station, stationId, position, true);
    public static GalaxyCombatTarget Npc(string npcId, Vector3 position)
    {
        return new GalaxyCombatTarget(
            CombatTargetType.Npc,
            npcId,
            position,
            true
        );
    }
}