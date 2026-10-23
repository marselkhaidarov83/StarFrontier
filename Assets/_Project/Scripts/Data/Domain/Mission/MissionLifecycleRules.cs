using System;
using System.Collections.Generic;

/// <summary>Saved mission contracts shared by migration and runtime services.</summary>
public static class MissionLifecycleRules
{
    public static bool IsPlannedType(MissionType type)
    {
        return type == MissionType.Delivery || type == MissionType.Recon ||
               type == MissionType.Defense || type == MissionType.Liberation ||
               type == MissionType.RouteSearch;
    }

    // Other planned types acquire their actual objective handlers in S08-02.
    public static bool IsPlayableType(MissionType type)
    {
        return type == MissionType.Delivery || type == MissionType.Recon;
    }

    public static bool IsActive(MissionStatus status)
    {
        return status == MissionStatus.Accepted || status == MissionStatus.InProgress ||
               status == MissionStatus.ReadyToComplete;
    }

    public static bool IsTerminal(MissionStatus status)
    {
        return status == MissionStatus.Completed || status == MissionStatus.Failed ||
               status == MissionStatus.Cancelled || status == MissionStatus.Expired;
    }

    public static bool IsExpired(MissionInstanceData mission, int currentDay)
    {
        return mission != null && mission.ExpiresAtDay > 0 && currentDay >= mission.ExpiresAtDay;
    }

    public static void EnsureCollections(RuntimeMissionSaveBlock block)
    {
        block.AvailableMissions ??= new List<MissionInstanceData>();
        block.ActiveMissions ??= new List<MissionInstanceData>();
        block.CompletedMissions ??= new List<MissionInstanceData>();
        block.ArchivedMissions ??= new List<MissionInstanceData>();
        block.OffersByPlanet ??= new Dictionary<string, PlanetOfferedMissionData>();
        block.OffersByPlanet_List ??= new List<PlanetOfferedMissionData>();
        block.RewardGrantedMissionIds ??= new List<string>();
    }

    public static void Normalize(RuntimeMissionSaveBlock block, int currentDay)
    {
        if (block == null)
            return;

        EnsureCollections(block);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // Successful/archived outcomes win over duplicate active and offered records.
        NormalizeList(block.CompletedMissions, seen);
        NormalizeList(block.ArchivedMissions, seen);
        NormalizeList(block.ActiveMissions, seen);
        NormalizeList(block.AvailableMissions, seen);

        // Old successful missions were paid by CompleteMission before being saved.
        // Seed the ledger to avoid a second payment when loading those records.
        var grants = new HashSet<string>(StringComparer.Ordinal);
        block.RewardGrantedMissionIds.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !grants.Add(id));
        foreach (var mission in block.CompletedMissions)
        {
            mission.Status = MissionStatus.Completed;
            mission.IsReadyToTurnIn = false;
            if (grants.Add(mission.MissionRuntimeId))
                block.RewardGrantedMissionIds.Add(mission.MissionRuntimeId);
        }

        // A paid mission cannot return to the board from a stale active/available entry.
        block.ActiveMissions.RemoveAll(mission => grants.Contains(mission.MissionRuntimeId));
        block.AvailableMissions.RemoveAll(mission => grants.Contains(mission.MissionRuntimeId));

        MoveTerminalOrDisabled(block, block.ActiveMissions, currentDay);
        MoveTerminalOrDisabled(block, block.AvailableMissions, currentDay);
        foreach (var mission in block.ArchivedMissions)
        {
            if (!IsTerminal(mission.Status) || mission.Status == MissionStatus.Completed)
            {
                mission.Status = MissionStatus.Cancelled;
                mission.LastTransitionReason = "InvalidArchivedState";
            }
            mission.IsReadyToTurnIn = false;
        }

        block.OffersByPlanet_List.RemoveAll(offer => offer == null || offer.OfferedMission == null ||
            !IsPlayableType(offer.OfferedMission.MissionType) ||
            offer.OfferedMission.Status != MissionStatus.Available ||
            seen.Contains(offer.OfferedMission.MissionRuntimeId) ||
            grants.Contains(offer.OfferedMission.MissionRuntimeId));
        var removeKeys = new List<string>();
        foreach (var pair in block.OffersByPlanet)
        {
            var mission = pair.Value?.OfferedMission;
            if (mission == null || !IsPlayableType(mission.MissionType) ||
                mission.Status != MissionStatus.Available ||
                block.ActiveMissions.Exists(item => item.MissionRuntimeId == mission.MissionRuntimeId) ||
                block.CompletedMissions.Exists(item => item.MissionRuntimeId == mission.MissionRuntimeId) ||
                block.ArchivedMissions.Exists(item => item.MissionRuntimeId == mission.MissionRuntimeId) ||
                grants.Contains(mission.MissionRuntimeId))
                removeKeys.Add(pair.Key);
        }
        foreach (var key in removeKeys)
            block.OffersByPlanet.Remove(key);

        // Keep only the payout ledger; completed mission payloads do not belong in saves.
        block.CompletedMissions.Clear();
    }

    private static void NormalizeList(List<MissionInstanceData> missions, HashSet<string> seen)
    {
        missions.RemoveAll(m => m == null || string.IsNullOrWhiteSpace(m.MissionRuntimeId) || !seen.Add(m.MissionRuntimeId));
        foreach (var mission in missions)
        {
            mission.DurationDays = Math.Max(0, mission.DurationDays);
            mission.AcceptedAtDay = Math.Max(0, mission.AcceptedAtDay);
            mission.ExpiresAtDay = Math.Max(0, mission.ExpiresAtDay);
            // ReadyToComplete is the canonical state; the boolean is its compatibility mirror.
            mission.IsReadyToTurnIn = mission.Status == MissionStatus.ReadyToComplete;
        }
    }

    private static void MoveTerminalOrDisabled(RuntimeMissionSaveBlock block, List<MissionInstanceData> source, int day)
    {
        for (int i = source.Count - 1; i >= 0; i--)
        {
            var mission = source[i];
            if (!IsTerminal(mission.Status) && IsPlayableType(mission.MissionType))
                continue;

            if (!IsTerminal(mission.Status))
            {
                mission.Status = MissionStatus.Cancelled;
                mission.LastTransitionReason = mission.MissionType == MissionType.Elimination
                    ? "LegacyEliminationDisabled" : "ObjectiveHandlerUnavailable";
                mission.FinishedAtDay = Math.Max(1, day);
            }
            source.RemoveAt(i);
            mission.IsReadyToTurnIn = false;
            if (mission.Status == MissionStatus.Completed)
            {
                block.CompletedMissions.Add(mission);
                if (!block.RewardGrantedMissionIds.Contains(mission.MissionRuntimeId))
                    block.RewardGrantedMissionIds.Add(mission.MissionRuntimeId);
            }
            else
                block.ArchivedMissions.Add(mission);
        }
    }
}
