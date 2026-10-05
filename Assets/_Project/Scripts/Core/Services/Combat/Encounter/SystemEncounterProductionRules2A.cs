using UnityEngine;

public enum SystemEncounterStartFailReason2A
{
    None = 0,

    EmptySystemId = 1,
    PlayerMissing = 2,
    PlayerInAnotherSystem = 3,
    PlayerOnPlanet = 4,
    PlayerShipMissing = 5,
    PlayerShipDestroyed = 6,
    EncounterAlreadyActive = 7,
    StarSystemMissing = 8,
    NoRouteContext = 9,
    SpawnRuleMissing = 10,
    SpawnRuleHasNoEnemies = 11
}

public static class SystemEncounterProductionRules2A
{
    public static bool CanStart(
        string systemId,
        PlayerState player,
        ShipRuntimeData activeShip,
        bool hasActiveEncounter,
        StarSystemConfig starSystem,
        EnemyGroupSpawnRuleConfig spawnRule,
        out SystemEncounterStartFailReason2A failReason)
    {
        if (string.IsNullOrWhiteSpace(systemId))
        {
            failReason =
                SystemEncounterStartFailReason2A.EmptySystemId;

            return false;
        }

        if (player == null)
        {
            failReason =
                SystemEncounterStartFailReason2A.PlayerMissing;

            return false;
        }

        if (player.CurrentSystemId != systemId)
        {
            failReason =
                SystemEncounterStartFailReason2A.PlayerInAnotherSystem;

            return false;
        }

        if (player.IsOnPlanet())
        {
            failReason =
                SystemEncounterStartFailReason2A.PlayerOnPlanet;

            return false;
        }

        if (activeShip == null)
        {
            failReason =
                SystemEncounterStartFailReason2A.PlayerShipMissing;

            return false;
        }

        if (activeShip.CurrentHull <= 0)
        {
            failReason =
                SystemEncounterStartFailReason2A.PlayerShipDestroyed;

            return false;
        }

        if (hasActiveEncounter)
        {
            failReason =
                SystemEncounterStartFailReason2A.EncounterAlreadyActive;

            return false;
        }

        if (starSystem == null)
        {
            failReason =
                SystemEncounterStartFailReason2A.StarSystemMissing;

            return false;
        }

        if (!HasRouteContext(starSystem))
        {
            failReason =
                SystemEncounterStartFailReason2A.NoRouteContext;

            return false;
        }

        if (spawnRule == null)
        {
            failReason =
                SystemEncounterStartFailReason2A.SpawnRuleMissing;

            return false;
        }

        if (!HasValidEnemyEntries(spawnRule))
        {
            failReason =
                SystemEncounterStartFailReason2A.SpawnRuleHasNoEnemies;

            return false;
        }

        failReason =
            SystemEncounterStartFailReason2A.None;

        return true;
    }

    public static bool HasRouteContext(
        StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return false;

        if (starSystem.Routes != null &&
            starSystem.Routes.Count > 0)
            return true;

        if (starSystem.LinkedSystems != null &&
            starSystem.LinkedSystems.Length > 0)
            return true;

        return false;
    }

    public static bool HasValidEnemyEntries(
        EnemyGroupSpawnRuleConfig spawnRule)
    {
        if (spawnRule == null)
            return false;

        return spawnRule.HasValidEnemies();
    }

    public static int CountEnemiesToSpawn(
        EnemyGroupSpawnRuleConfig spawnRule)
    {
        if (spawnRule == null)
            return 0;

        return spawnRule.GetMaxEnemyCount();
    }
}