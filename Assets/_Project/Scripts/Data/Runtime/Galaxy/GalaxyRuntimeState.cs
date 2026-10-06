using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class GalaxyRuntimeState
{
    public int GalaxyDay;
    public string CurrentSystemId;

    public List<SectorRuntimeState> Sectors = new();
    public List<StarSystemRuntimeState> Systems = new();
    public List<RouteRuntimeState> Routes = new();

    public List<EnemyFactionState> EnemyFactions = new();
    public List<EnemyGroupState> EnemyGroups = new();
    public List<InvasionState> Invasions = new();

    public void EnsureWarStateCollections()
    {
        EnemyFactions ??= new List<EnemyFactionState>();
        EnemyGroups ??= new List<EnemyGroupState>();
        Invasions ??= new List<InvasionState>();
    }

    public int CountActiveInvasions()
    {
        EnsureWarStateCollections();

        return Invasions.Count(
            invasion =>
                invasion != null &&
                invasion.IsActive());
    }

    public bool HasActiveInvasionForTarget(
        string targetSystemId)
    {
        if (string.IsNullOrWhiteSpace(targetSystemId))
            return false;

        EnsureWarStateCollections();

        return Invasions.Any(
            invasion =>
                invasion != null &&
                invasion.TargetSystemId == targetSystemId &&
                invasion.IsActive());
    }

    public bool CanStartAdditionalInvasion(
        string targetSystemId,
        int maxActiveInvasions)
    {
        if (string.IsNullOrWhiteSpace(targetSystemId))
            return false;

        if (maxActiveInvasions <= 0)
            return false;

        if (HasActiveInvasionForTarget(targetSystemId))
            return false;

        return CountActiveInvasions() < maxActiveInvasions;
    }

    public bool CanCaptureSystemWithoutHopelessCollapse(
        string targetSystemId)
    {
        if (string.IsNullOrWhiteSpace(targetSystemId))
            return false;

        if (Systems == null || Systems.Count == 0)
            return false;

        int recoverableSystemsAfterCapture = 0;

        for (int i = 0; i < Systems.Count; i++)
        {
            StarSystemRuntimeState system =
                Systems[i];

            if (system == null)
                continue;

            bool targetWouldBeCaptured =
                system.SystemId == targetSystemId;

            if (targetWouldBeCaptured)
                continue;

            if (system.SystemStatus != StarSystemStatus.Captured)
                recoverableSystemsAfterCapture++;
        }

        return recoverableSystemsAfterCapture > 0;
    }

    public bool RepairHopelessCollapse()
    {
        if (Systems == null || Systems.Count == 0)
            return false;

        StarSystemRuntimeState firstCapturedSystem =
            null;

        for (int i = 0; i < Systems.Count; i++)
        {
            StarSystemRuntimeState system =
                Systems[i];

            if (system == null)
                continue;

            if (system.SystemStatus != StarSystemStatus.Captured)
                return false;

            firstCapturedSystem ??= system;
        }

        if (firstCapturedSystem == null)
            return false;

        firstCapturedSystem.MarkRecoveryReady();
        return true;
    }

    public int ProcessOfflineWarCatchUp(
        int targetQuantTick)
    {
        EnsureWarStateCollections();

        if (targetQuantTick <= 0)
            return 0;

        int changedCount = 0;

        for (int i = 0; i < Invasions.Count; i++)
        {
            InvasionState invasion =
                Invasions[i];

            if (invasion == null)
                continue;

            if (!invasion.IsActive())
                continue;

            if (invasion.ResolveAtTick <= 0 ||
                invasion.ResolveAtTick > targetQuantTick)
            {
                invasion.LastUpdatedTick =
                    Math.Max(
                        invasion.LastUpdatedTick,
                        targetQuantTick);

                invasion.NextUpdateTick =
                    Math.Max(
                        invasion.NextUpdateTick,
                        targetQuantTick + 1);

                continue;
            }

            ResolveInvasionByOfflineCatchUp(
                invasion,
                targetQuantTick);

            changedCount++;
        }

        changedCount += ProcessOfflineSystemWarDegradation(
            targetQuantTick);

        if (RepairHopelessCollapse())
            changedCount++;

        return changedCount;
    }

    private void ResolveInvasionByOfflineCatchUp(
        InvasionState invasion,
        int targetQuantTick)
    {
        invasion.LifecycleState =
            InvasionLifecycleState.Resolved;

        invasion.LastUpdatedTick =
            targetQuantTick;

        invasion.CleanupAfterTick =
            targetQuantTick + 1;

        StarSystemRuntimeState targetSystem =
            FindSystemState(invasion.TargetSystemId);

        if (targetSystem == null)
            return;

        bool captureAllowed =
            CanCaptureSystemWithoutHopelessCollapse(
                invasion.TargetSystemId);

        if (captureAllowed)
        {
            targetSystem.MarkCaptured();
            AddOwnedSystemToFaction(
                invasion.FactionId,
                invasion.TargetSystemId);
        }
        else
        {
            targetSystem.MarkRecoveryReady();
        }
    }

    private int ProcessOfflineSystemWarDegradation(
        int targetQuantTick)
    {
        if (Systems == null)
            return 0;

        int changedCount = 0;

        for (int i = 0; i < Systems.Count; i++)
        {
            StarSystemRuntimeState system =
                Systems[i];

            if (system == null)
                continue;

            StarSystemStatus previousStatus =
                system.SystemStatus;

            int previousStability =
                system.Stability;

            bool hasActiveInvasion =
                HasActiveInvasionForTarget(
                    system.SystemId);

            if (!hasActiveInvasion)
            {
                system.ApplyOfflineWarDegradation(
                    targetQuantTick);
            }

            if (system.SystemStatus == StarSystemStatus.Invasion &&
                !hasActiveInvasion &&
                targetQuantTick >= 3)
            {
                if (CanCaptureSystemWithoutHopelessCollapse(system.SystemId))
                {
                    system.MarkCaptured();
                }
                else
                {
                    system.MarkRecoveryReady();
                }
            }

            if (previousStatus != system.SystemStatus ||
                previousStability != system.Stability)
            {
                changedCount++;
            }
        }

        return changedCount;
    }

    private StarSystemRuntimeState FindSystemState(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId) ||
            Systems == null)
        {
            return null;
        }

        return Systems.FirstOrDefault(
            system =>
                system != null &&
                system.SystemId == systemId);
    }

    private void AddOwnedSystemToFaction(
        string factionId,
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(factionId) ||
            string.IsNullOrWhiteSpace(systemId))
        {
            return;
        }

        EnsureWarStateCollections();

        EnemyFactionState faction =
            EnemyFactions.FirstOrDefault(
                item =>
                    item != null &&
                    item.FactionId == factionId);

        if (faction == null)
        {
            faction =
                new EnemyFactionState
                {
                    FactionId = factionId,
                    DisplayName = factionId
                };

            EnemyFactions.Add(faction);
        }

        faction.EnsureWarTerritoryLists();

        if (!faction.OwnedSystemIds.Contains(systemId))
            faction.OwnedSystemIds.Add(systemId);

        if (!faction.TerritorySystemIds.Contains(systemId))
            faction.TerritorySystemIds.Add(systemId);
    }
}