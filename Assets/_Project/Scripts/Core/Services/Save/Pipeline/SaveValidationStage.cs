using System.Collections.Generic;

public sealed class SaveValidationStage
{
    public SaveValidationResult ValidateVersion(GameRuntimeState state)
    {
        var result = new SaveValidationResult();

        if (state == null)
        {
            result.Errors.Add("Save state is null.");
            return result;
        }

        if (state.Meta != null &&
            state.Meta.SaveDataVersion > SaveDataVersions.Current)
        {
            result.Errors.Add(
                "Unsupported future SaveDataVersion: " +
                state.Meta.SaveDataVersion + ".");
        }

        return result;
    }

    public SaveValidationResult ValidateAndNormalize(GameRuntimeState state)
    {
        SaveValidationResult result = ValidateVersion(state);

        if (!result.IsValid || state == null)
            return result;

        SaveMigrationService.Migrate(state);

        if (state.Meta == null)
            result.Errors.Add("Meta block is missing.");

        if (state.Player == null)
        {
            result.Errors.Add("Player block is missing.");
            return result;
        }

        if (state.Galaxy == null)
            result.Errors.Add("Galaxy block is missing.");

        if (state.Settings == null)
            result.Errors.Add("Settings block is missing.");

        if (state.MissionBlock == null)
            result.Errors.Add("Mission block is missing.");

        if (state.SystemEncounter == null)
            result.Errors.Add("System encounter block is missing.");

        if (state.SystemNpcSimulation == null)
            result.Errors.Add("System NPC simulation block is missing.");

        state.Markets ??= new List<MarketRuntimeData>();

        if (state.Player.Level < 1)
        {
            state.Player.Level = 1;
            result.Normalizations.Add("Player.Level was clamped to 1.");
        }

        if (state.Player.Experience < 0)
        {
            state.Player.Experience = 0;
            result.Normalizations.Add("Player.Experience was clamped to 0.");
        }

        if (state.Player.Credits < 0)
        {
            state.Player.Credits = 0;
            result.Normalizations.Add("Player.Credits was clamped to 0.");
        }

        NormalizeMissionCollections(state, result);
        NormalizeNpcCollections(state, result);
        NormalizeWarCollections(state, result);

        return result;
    }

    private static void NormalizeWarCollections(
        GameRuntimeState state,
        SaveValidationResult result)
    {
        if (state.Galaxy == null)
            return;

        state.Galaxy.EnsureWarStateCollections();

        int removedFactionCount =
            state.Galaxy.EnemyFactions.RemoveAll(
                faction =>
                    faction == null ||
                    string.IsNullOrWhiteSpace(faction.FactionId));

        int removedGroupCount =
            state.Galaxy.EnemyGroups.RemoveAll(
                group =>
                    group == null ||
                    string.IsNullOrWhiteSpace(group.RuntimeGroupId));

        int removedInvasionCount =
            state.Galaxy.Invasions.RemoveAll(
                invasion =>
                    invasion == null ||
                    string.IsNullOrWhiteSpace(invasion.InvasionId));

        NormalizeFactionStates(state.Galaxy.EnemyFactions);
        NormalizeEnemyGroupStates(state.Galaxy.EnemyGroups);
        NormalizeInvasionStates(state.Galaxy.Invasions);

        int cancelledDuplicateInvasions =
            CancelDuplicateActiveInvasions(state.Galaxy.Invasions);

        bool repairedHopelessCollapse =
            state.Galaxy.RepairHopelessCollapse();

        if (removedFactionCount > 0)
        {
            result.Normalizations.Add(
                "Removed invalid enemy faction entries: " +
                removedFactionCount +
                ".");
        }

        if (removedGroupCount > 0)
        {
            result.Normalizations.Add(
                "Removed invalid enemy group entries: " +
                removedGroupCount +
                ".");
        }

        if (removedInvasionCount > 0)
        {
            result.Normalizations.Add(
                "Removed invalid invasion entries: " +
                removedInvasionCount +
                ".");
        }

        if (cancelledDuplicateInvasions > 0)
        {
            result.Normalizations.Add(
                "Cancelled duplicate active invasions: " +
                cancelledDuplicateInvasions +
                ".");
        }

        if (repairedHopelessCollapse)
        {
            result.Normalizations.Add(
                "Repaired hopeless galaxy collapse by restoring one system to RecoveryReady.");
        }
    }

    private static void NormalizeFactionStates(
        List<EnemyFactionState> factions)
    {
        if (factions == null)
            return;

        HashSet<string> seenFactionIds =
            new HashSet<string>();

        for (int i = factions.Count - 1; i >= 0; i--)
        {
            EnemyFactionState faction =
                factions[i];

            if (faction == null ||
                string.IsNullOrWhiteSpace(faction.FactionId))
            {
                factions.RemoveAt(i);
                continue;
            }

            faction.FactionId =
                faction.FactionId.Trim().ToLowerInvariant();

            if (!seenFactionIds.Add(faction.FactionId))
            {
                factions.RemoveAt(i);
                continue;
            }

            faction.OwnedSystemIds ??= new List<string>();
            faction.TerritorySystemIds ??= new List<string>();
            faction.FrontlineSystemIds ??= new List<string>();
            faction.FrontierSystemIds ??= new List<string>();
            faction.ActiveGroupRuntimeIds ??= new List<string>();
            faction.ActiveInvasionIds ??= new List<string>();

            RemoveEmptyAndDuplicateIds(faction.OwnedSystemIds);
            RemoveEmptyAndDuplicateIds(faction.TerritorySystemIds);
            RemoveEmptyAndDuplicateIds(faction.FrontlineSystemIds);
            RemoveEmptyAndDuplicateIds(faction.FrontierSystemIds);
            RemoveEmptyAndDuplicateIds(faction.ActiveGroupRuntimeIds);
            RemoveEmptyAndDuplicateIds(faction.ActiveInvasionIds);
        }
    }

    private static void NormalizeEnemyGroupStates(
        List<EnemyGroupState> groups)
    {
        if (groups == null)
            return;

        HashSet<string> seenGroupIds =
            new HashSet<string>();

        for (int i = groups.Count - 1; i >= 0; i--)
        {
            EnemyGroupState group =
                groups[i];

            if (group == null ||
                string.IsNullOrWhiteSpace(group.RuntimeGroupId))
            {
                groups.RemoveAt(i);
                continue;
            }

            if (!seenGroupIds.Add(group.RuntimeGroupId))
            {
                groups.RemoveAt(i);
                continue;
            }

            group.Level =
                UnityEngine.Mathf.Clamp(group.Level, 1, 10);

            if (!System.Enum.IsDefined(
                    typeof(EnemyGroupLifecycleState),
                    group.LifecycleState))
            {
                group.LifecycleState =
                    EnemyGroupLifecycleState.None;
            }

            group.MemberRuntimeNpcIds ??= new List<string>();
            group.MemberConfigIds ??= new List<string>();

            RemoveEmptyAndDuplicateIds(group.MemberRuntimeNpcIds);
            RemoveEmptyAndDuplicateIds(group.MemberConfigIds);
        }
    }

    private static void NormalizeInvasionStates(
        List<InvasionState> invasions)
    {
        if (invasions == null)
            return;

        HashSet<string> seenInvasionIds =
            new HashSet<string>();

        for (int i = invasions.Count - 1; i >= 0; i--)
        {
            InvasionState invasion =
                invasions[i];

            if (invasion == null ||
                string.IsNullOrWhiteSpace(invasion.InvasionId))
            {
                invasions.RemoveAt(i);
                continue;
            }

            if (!seenInvasionIds.Add(invasion.InvasionId))
            {
                invasions.RemoveAt(i);
                continue;
            }

            invasion.Level =
                UnityEngine.Mathf.Clamp(invasion.Level, 1, 10);

            invasion.ApplyEscalation(
                invasion.FactionId,
                invasion.Level);

            if (!System.Enum.IsDefined(
                    typeof(InvasionLifecycleState),
                    invasion.LifecycleState))
            {
                invasion.LifecycleState =
                    InvasionLifecycleState.None;
            }

            invasion.EnemyGroupRuntimeIds ??= new List<string>();

            RemoveEmptyAndDuplicateIds(
                invasion.EnemyGroupRuntimeIds);
        }
    }

    private static int CancelDuplicateActiveInvasions(
        List<InvasionState> invasions)
    {
        if (invasions == null)
            return 0;

        HashSet<string> activeTargetSystemIds =
            new HashSet<string>();

        int cancelledCount = 0;

        for (int i = 0; i < invasions.Count; i++)
        {
            InvasionState invasion =
                invasions[i];

            if (invasion == null)
                continue;

            if (!invasion.IsActive())
                continue;

            if (string.IsNullOrWhiteSpace(invasion.TargetSystemId))
            {
                invasion.LifecycleState =
                    InvasionLifecycleState.Cancelled;

                cancelledCount++;
                continue;
            }

            if (activeTargetSystemIds.Add(invasion.TargetSystemId))
                continue;

            invasion.LifecycleState =
                InvasionLifecycleState.Cancelled;

            cancelledCount++;
        }

        return cancelledCount;
    }

    private static void RemoveEmptyAndDuplicateIds(
        List<string> ids)
    {
        if (ids == null)
            return;

        HashSet<string> seenIds =
            new HashSet<string>();

        for (int i = ids.Count - 1; i >= 0; i--)
        {
            string id =
                ids[i];

            if (string.IsNullOrWhiteSpace(id))
            {
                ids.RemoveAt(i);
                continue;
            }

            id = id.Trim();
            ids[i] = id;

            if (!seenIds.Add(id))
                ids.RemoveAt(i);
        }
    }

    private static void NormalizeMissionCollections(
        GameRuntimeState state,
        SaveValidationResult result)
    {
        if (state.MissionBlock == null)
            return;

        state.MissionBlock.OffersByPlanet ??= new();
        state.MissionBlock.OffersByPlanet_List ??= new();
        state.MissionBlock.AvailableMissions ??= new();
        state.MissionBlock.ActiveMissions ??= new();
        state.MissionBlock.CompletedMissions ??= new();
        state.MissionBlock.RewardGrantedMissionIds ??= new();

        int removed =
            state.MissionBlock.AvailableMissions.RemoveAll(item => item == null) +
            state.MissionBlock.ActiveMissions.RemoveAll(item => item == null) +
            state.MissionBlock.CompletedMissions.RemoveAll(item => item == null) +
            state.MissionBlock.OffersByPlanet_List.RemoveAll(item => item == null);

        if (removed > 0)
        {
            result.Normalizations.Add(
                "Removed null mission entries: " + removed + ".");
        }
    }

    private static void NormalizeNpcCollections(
    GameRuntimeState state,
    SaveValidationResult result)
    {
        if (state.SystemNpcSimulation == null)
            return;

        state.SystemNpcSimulation.PopulationEntries ??= new();
        state.SystemNpcSimulation.Npcs ??= new();
        state.SystemNpcSimulation.PopulationTimers ??= new();

        int removedPopulationEntries =
            state.SystemNpcSimulation.PopulationEntries.RemoveAll(item => item == null);

        int removedInvalidPopulationEntries =
            state.SystemNpcSimulation.PopulationEntries.RemoveAll(item =>
                item.Count <= 0 ||
                string.IsNullOrWhiteSpace(item.SystemId) ||
                string.IsNullOrWhiteSpace(item.ConfigId));

        int removedNpcs =
            state.SystemNpcSimulation.Npcs.RemoveAll(item => item == null);

        if (removedPopulationEntries > 0)
        {
            result.Normalizations.Add(
                "Removed null compact NPC population entries: " + removedPopulationEntries + ".");
        }

        if (removedInvalidPopulationEntries > 0)
        {
            result.Normalizations.Add(
                "Removed invalid compact NPC population entries: " + removedInvalidPopulationEntries + ".");
        }

        if (removedNpcs > 0)
        {
            result.Normalizations.Add(
                "Removed null legacy NPC entries: " + removedNpcs + ".");
        }
    }
}

public sealed class SaveValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Normalizations { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public string BuildErrorMessage()
    {
        return string.Join(" ", Errors);
    }
}
