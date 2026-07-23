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

        return result;
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

        state.SystemNpcSimulation.Npcs ??= new();
        state.SystemNpcSimulation.PopulationTimers ??= new();

        int removedNpcs =
            state.SystemNpcSimulation.Npcs.RemoveAll(item => item == null);

        if (removedNpcs > 0)
        {
            result.Normalizations.Add(
                "Removed null NPC entries: " + removedNpcs + ".");
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
