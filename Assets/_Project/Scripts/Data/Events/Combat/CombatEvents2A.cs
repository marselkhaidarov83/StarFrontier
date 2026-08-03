using UnityEngine;

public struct CombatRuntimeStartedEvent2A
{
    public string EncounterId { get; }
    public string SystemId { get; }
    public int EnemyCount { get; }
    public int AllyCount { get; }

    public CombatRuntimeStartedEvent2A(
        string encounterId,
        string systemId,
        int enemyCount,
        int allyCount)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        EnemyCount = enemyCount;
        AllyCount = allyCount;
    }
}

public struct CombatDamageEvent2A
{
    public string TargetId { get; }
    public bool IsPlayerTarget { get; }
    public int Damage { get; }
    public int CurrentShield { get; }
    public int CurrentHull { get; }

    public CombatDamageEvent2A(
        string targetId,
        bool isPlayerTarget,
        int damage,
        int currentShield,
        int currentHull)
    {
        TargetId = targetId;
        IsPlayerTarget = isPlayerTarget;
        Damage = damage;
        CurrentShield = currentShield;
        CurrentHull = currentHull;
    }
}

public struct CombatProjectileCreatedEvent2A
{
    public string ProjectileId { get; }
    public string SystemId { get; }
    public string ShooterId { get; }
    public string TargetId { get; }
    public string WeaponConfigId { get; }
    public Vector3 StartPosition { get; }
    public Vector3 TargetPosition { get; }

    public CombatProjectileCreatedEvent2A(
        string projectileId,
        string systemId,
        string shooterId,
        string targetId,
        string weaponConfigId,
        Vector3 startPosition,
        Vector3 targetPosition)
    {
        ProjectileId = projectileId;
        SystemId = systemId;
        ShooterId = shooterId;
        TargetId = targetId;
        WeaponConfigId = weaponConfigId;
        StartPosition = startPosition;
        TargetPosition = targetPosition;
    }
}

public struct CombatProjectileImpactEvent2A
{
    public string ProjectileId { get; }
    public string SystemId { get; }
    public string TargetId { get; }
    public int Damage { get; }
    public Vector3 HitPosition { get; }
    public bool DidHit { get; }

    public CombatProjectileImpactEvent2A(
        string projectileId,
        string systemId,
        string targetId,
        int damage,
        Vector3 hitPosition,
        bool didHit)
    {
        ProjectileId = projectileId;
        SystemId = systemId;
        TargetId = targetId;
        Damage = damage;
        HitPosition = hitPosition;
        DidHit = didHit;
    }
}

public struct CombatTargetDestroyedEvent2A
{
    public string EncounterId { get; }
    public string SystemId { get; }
    public string TargetType { get; }
    public string DestroyedBy { get; }

    public CombatTargetDestroyedEvent2A(
        string encounterId,
        string systemId,
        string targetType,
        string destroyedBy)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        TargetType = targetType;
        DestroyedBy = destroyedBy;
    }
}

public struct CombatVictoryEvent2A
{
    public string EncounterId { get; }
    public string SystemId { get; }
    public int PlayerKills { get; }

    public CombatVictoryEvent2A(
        string encounterId,
        string systemId,
        int playerKills)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        PlayerKills = playerKills;
    }
}

public struct CombatDefeatEvent2A
{
    public string EncounterId { get; }
    public string SystemId { get; }
    public SystemEncounterDefeatReason Reason { get; }

    public CombatDefeatEvent2A(
        string encounterId,
        string systemId,
        SystemEncounterDefeatReason reason)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        Reason = reason;
    }
}

public struct CombatRewardPendingEvent2A
{
    public string EncounterId { get; }
    public string SystemId { get; }
    public int PlayerKills { get; }

    public CombatRewardPendingEvent2A(
        string encounterId,
        string systemId,
        int playerKills)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        PlayerKills = playerKills;
    }
}