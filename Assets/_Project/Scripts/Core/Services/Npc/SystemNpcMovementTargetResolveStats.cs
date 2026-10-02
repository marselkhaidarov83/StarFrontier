using UnityEngine;

public sealed class SystemNpcMovementTargetResolveStats
{
    public int SystemExitLogCount;
    public int SystemExitInvalidPointCheckCount;
    public int SystemExitReturnCount;
    public int SystemExitResetCount;

    public double SystemExitLogMs;
    public double SystemExitInvalidPointCheckMs;
    public double SystemExitReturnMs;
    public double SystemExitResetMs;

    public int GetNextTargetCount;
    public int GetNextTargetAllyCount;
    public int GetNextTargetPirateCount;
    public int GetNextTargetEnemyCount;
    public int GetNextTargetFallbackCount;

    public int CombatNpcCheckCount;
    public int SystemExitCheckCount;
    public int PlanetLookupCount;
    public int PlanetCacheCheckCount;
    public int PlanetCacheHitCount;
    public int PlanetCacheMissCount;
    public int PlanetConfigLookupCount;
    public int PlanetOrbitalPositionCount;
    public int PlanetInvalidPointCheckCount;

    public int TargetPositionCheckCount;
    public int FallbackCount;
    public int EnemyNearestAllyCheckCount;
    public int PlayerCheckCount;
    public int NearestInhabitedPlanetCheckCount;
    public int CombatApproachCount;
    public int ApproachPositionCount;
    public int InvalidSystemPointCheckCount;

    public double GetNextTargetMs;
    public double GetNextTargetAllyMs;
    public double GetNextTargetPirateMs;
    public double GetNextTargetEnemyMs;
    public double GetNextTargetFallbackMs;

    public double CombatNpcCheckMs;
    public double SystemExitCheckMs;
    public double PlanetLookupMs;
    public double PlanetCacheCheckMs;
    public double PlanetConfigLookupMs;
    public double PlanetOrbitalPositionMs;
    public double PlanetInvalidPointCheckMs;

    public double TargetPositionCheckMs;
    public double FallbackMs;
    public double EnemyNearestAllyCheckMs;
    public double PlayerCheckMs;
    public double NearestInhabitedPlanetCheckMs;
    public double CombatApproachMs;
    public double ApproachPositionMs;
    public double InvalidSystemPointCheckMs;

    public void Reset()
    {
        SystemExitLogCount = 0;
        SystemExitInvalidPointCheckCount = 0;
        SystemExitReturnCount = 0;
        SystemExitResetCount = 0;

        SystemExitLogMs = 0d;
        SystemExitInvalidPointCheckMs = 0d;
        SystemExitReturnMs = 0d;
        SystemExitResetMs = 0d;

        GetNextTargetCount = 0;
        GetNextTargetAllyCount = 0;
        GetNextTargetPirateCount = 0;
        GetNextTargetEnemyCount = 0;
        GetNextTargetFallbackCount = 0;

        CombatNpcCheckCount = 0;
        SystemExitCheckCount = 0;
        PlanetLookupCount = 0;
        PlanetCacheCheckCount = 0;
        PlanetCacheHitCount = 0;
        PlanetCacheMissCount = 0;
        PlanetConfigLookupCount = 0;
        PlanetOrbitalPositionCount = 0;
        PlanetInvalidPointCheckCount = 0;

        TargetPositionCheckCount = 0;
        FallbackCount = 0;
        EnemyNearestAllyCheckCount = 0;
        PlayerCheckCount = 0;
        NearestInhabitedPlanetCheckCount = 0;
        CombatApproachCount = 0;
        ApproachPositionCount = 0;
        InvalidSystemPointCheckCount = 0;

        GetNextTargetMs = 0d;
        GetNextTargetAllyMs = 0d;
        GetNextTargetPirateMs = 0d;
        GetNextTargetEnemyMs = 0d;
        GetNextTargetFallbackMs = 0d;

        CombatNpcCheckMs = 0d;
        SystemExitCheckMs = 0d;
        PlanetLookupMs = 0d;
        PlanetCacheCheckMs = 0d;
        PlanetConfigLookupMs = 0d;
        PlanetOrbitalPositionMs = 0d;
        PlanetInvalidPointCheckMs = 0d;

        TargetPositionCheckMs = 0d;
        FallbackMs = 0d;
        EnemyNearestAllyCheckMs = 0d;
        PlayerCheckMs = 0d;
        NearestInhabitedPlanetCheckMs = 0d;
        CombatApproachMs = 0d;
        ApproachPositionMs = 0d;
        InvalidSystemPointCheckMs = 0d;
    }

    public void Add(SystemNpcMovementTargetResolveStats other)
    {
        if (other == null)
            return;

        SystemExitLogCount += other.SystemExitLogCount;
        SystemExitInvalidPointCheckCount += other.SystemExitInvalidPointCheckCount;
        SystemExitReturnCount += other.SystemExitReturnCount;
        SystemExitResetCount += other.SystemExitResetCount;

        SystemExitLogMs += other.SystemExitLogMs;
        SystemExitInvalidPointCheckMs += other.SystemExitInvalidPointCheckMs;
        SystemExitReturnMs += other.SystemExitReturnMs;
        SystemExitResetMs += other.SystemExitResetMs;

        GetNextTargetCount += other.GetNextTargetCount;
        GetNextTargetAllyCount += other.GetNextTargetAllyCount;
        GetNextTargetPirateCount += other.GetNextTargetPirateCount;
        GetNextTargetEnemyCount += other.GetNextTargetEnemyCount;
        GetNextTargetFallbackCount += other.GetNextTargetFallbackCount;

        CombatNpcCheckCount += other.CombatNpcCheckCount;
        SystemExitCheckCount += other.SystemExitCheckCount;
        PlanetLookupCount += other.PlanetLookupCount;
        PlanetCacheCheckCount += other.PlanetCacheCheckCount;
        PlanetCacheHitCount += other.PlanetCacheHitCount;
        PlanetCacheMissCount += other.PlanetCacheMissCount;
        PlanetConfigLookupCount += other.PlanetConfigLookupCount;
        PlanetOrbitalPositionCount += other.PlanetOrbitalPositionCount;
        PlanetInvalidPointCheckCount += other.PlanetInvalidPointCheckCount;

        TargetPositionCheckCount += other.TargetPositionCheckCount;
        FallbackCount += other.FallbackCount;
        EnemyNearestAllyCheckCount += other.EnemyNearestAllyCheckCount;
        PlayerCheckCount += other.PlayerCheckCount;
        NearestInhabitedPlanetCheckCount += other.NearestInhabitedPlanetCheckCount;
        CombatApproachCount += other.CombatApproachCount;
        ApproachPositionCount += other.ApproachPositionCount;
        InvalidSystemPointCheckCount += other.InvalidSystemPointCheckCount;

        GetNextTargetMs += other.GetNextTargetMs;
        GetNextTargetAllyMs += other.GetNextTargetAllyMs;
        GetNextTargetPirateMs += other.GetNextTargetPirateMs;
        GetNextTargetEnemyMs += other.GetNextTargetEnemyMs;
        GetNextTargetFallbackMs += other.GetNextTargetFallbackMs;

        CombatNpcCheckMs += other.CombatNpcCheckMs;
        SystemExitCheckMs += other.SystemExitCheckMs;
        PlanetLookupMs += other.PlanetLookupMs;
        PlanetCacheCheckMs += other.PlanetCacheCheckMs;
        PlanetConfigLookupMs += other.PlanetConfigLookupMs;
        PlanetOrbitalPositionMs += other.PlanetOrbitalPositionMs;
        PlanetInvalidPointCheckMs += other.PlanetInvalidPointCheckMs;

        TargetPositionCheckMs += other.TargetPositionCheckMs;
        FallbackMs += other.FallbackMs;
        EnemyNearestAllyCheckMs += other.EnemyNearestAllyCheckMs;
        PlayerCheckMs += other.PlayerCheckMs;
        NearestInhabitedPlanetCheckMs += other.NearestInhabitedPlanetCheckMs;
        CombatApproachMs += other.CombatApproachMs;
        ApproachPositionMs += other.ApproachPositionMs;
        InvalidSystemPointCheckMs += other.InvalidSystemPointCheckMs;
    }

    public void CopyFrom(SystemNpcMovementTargetResolveStats other)
    {
        Reset();
        Add(other);
    }
}