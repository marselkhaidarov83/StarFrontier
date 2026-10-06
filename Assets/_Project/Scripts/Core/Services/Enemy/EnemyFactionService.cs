using System;
using System.Collections.Generic;
using System.Linq;

public sealed class EnemyFactionService :
    CustomService,
    IEnemyFactionService
{
    private const string AiFactionId = "ai";
    private const string AncientsFactionId = "ancients";
    private const string InfectedFactionId = "infected";

    private readonly IGameSessionService _gameSessionService;

    public EnemyFactionService()
    {
        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameSessionService>();

        RepairFactionState();
    }

    public IReadOnlyList<EnemyFactionState> GetFactions()
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return Array.Empty<EnemyFactionState>();

        RepairFactionState();

        return galaxyState.EnemyFactions;
    }

    public bool TryGetFaction(
        string factionId,
        out EnemyFactionState factionState)
    {
        factionState = null;

        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return false;

        string normalizedFactionId =
            NormalizeFactionId(factionId);

        if (string.IsNullOrWhiteSpace(normalizedFactionId))
            return false;

        RepairFactionState();

        factionState =
            galaxyState.EnemyFactions.FirstOrDefault(
                faction =>
                    faction != null &&
                    NormalizeFactionId(faction.FactionId) == normalizedFactionId);

        return factionState != null;
    }

    public EnemyFactionState GetOrCreateFaction(
        string factionId,
        string displayName)
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return null;

        string normalizedFactionId =
            NormalizeFactionId(factionId);

        if (string.IsNullOrWhiteSpace(normalizedFactionId))
            return null;

        RepairFactionState();

        if (TryGetFaction(normalizedFactionId, out EnemyFactionState existingFaction))
            return existingFaction;

        EnemyFactionState factionState =
            new EnemyFactionState
            {
                FactionId = normalizedFactionId,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? normalizedFactionId
                    : displayName,
                OwnedSystemIds = new List<string>(),
                TerritorySystemIds = new List<string>(),
                ActiveGroupRuntimeIds = new List<string>(),
                ActiveInvasionIds = new List<string>()
            };

        galaxyState.EnemyFactions.Add(factionState);

        return factionState;
    }

    public bool RegisterFaction(
        string factionId,
        string displayName)
    {
        return GetOrCreateFaction(factionId, displayName) != null;
    }

    public bool FactionOwnsSystem(
        string factionId,
        string systemId)
    {
        if (!TryGetFaction(factionId, out EnemyFactionState factionState))
            return false;

        return ContainsId(factionState.OwnedSystemIds, systemId);
    }

    public bool HasTerritoryAccess(
        string factionId,
        string systemId)
    {
        if (!TryGetFaction(factionId, out EnemyFactionState factionState))
            return false;

        return ContainsId(factionState.OwnedSystemIds, systemId) ||
               ContainsId(factionState.TerritorySystemIds, systemId);
    }

    public bool AddOwnedSystem(
        string factionId,
        string systemId)
    {
        EnemyFactionState factionState =
            GetOrCreateFaction(factionId, factionId);

        if (factionState == null ||
            string.IsNullOrWhiteSpace(systemId))
        {
            return false;
        }

        EnsureFactionLists(factionState);

        AddUniqueId(factionState.OwnedSystemIds, systemId);
        AddUniqueId(factionState.TerritorySystemIds, systemId);

        return true;
    }

    public bool RemoveOwnedSystem(
        string factionId,
        string systemId)
    {
        if (!TryGetFaction(factionId, out EnemyFactionState factionState))
            return false;

        RemoveId(factionState.OwnedSystemIds, systemId);
        return true;
    }

    public bool AddTerritorySystem(
        string factionId,
        string systemId)
    {
        EnemyFactionState factionState =
            GetOrCreateFaction(factionId, factionId);

        if (factionState == null ||
            string.IsNullOrWhiteSpace(systemId))
        {
            return false;
        }

        EnsureFactionLists(factionState);
        AddUniqueId(factionState.TerritorySystemIds, systemId);

        return true;
    }

    public bool RemoveTerritorySystem(
        string factionId,
        string systemId)
    {
        if (!TryGetFaction(factionId, out EnemyFactionState factionState))
            return false;

        RemoveId(factionState.TerritorySystemIds, systemId);
        return true;
    }

    public bool RegisterEnemyGroup(
        string factionId,
        string enemyGroupRuntimeId)
    {
        EnemyFactionState factionState =
            GetOrCreateFaction(factionId, factionId);

        if (factionState == null ||
            string.IsNullOrWhiteSpace(enemyGroupRuntimeId))
        {
            return false;
        }

        EnsureFactionLists(factionState);
        AddUniqueId(factionState.ActiveGroupRuntimeIds, enemyGroupRuntimeId);

        return true;
    }

    public bool UnregisterEnemyGroup(
        string factionId,
        string enemyGroupRuntimeId)
    {
        if (!TryGetFaction(factionId, out EnemyFactionState factionState))
            return false;

        RemoveId(factionState.ActiveGroupRuntimeIds, enemyGroupRuntimeId);
        return true;
    }

    public bool RegisterInvasion(
        string factionId,
        string invasionId)
    {
        EnemyFactionState factionState =
            GetOrCreateFaction(factionId, factionId);

        if (factionState == null ||
            string.IsNullOrWhiteSpace(invasionId))
        {
            return false;
        }

        EnsureFactionLists(factionState);
        AddUniqueId(factionState.ActiveInvasionIds, invasionId);

        return true;
    }

    public bool UnregisterInvasion(
        string factionId,
        string invasionId)
    {
        if (!TryGetFaction(factionId, out EnemyFactionState factionState))
            return false;

        RemoveId(factionState.ActiveInvasionIds, invasionId);
        return true;
    }

    public void RepairFactionState()
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return;

        galaxyState.EnsureWarStateCollections();

        RepairFactionList(galaxyState);

        RegisterDefaultFaction(
            AiFactionId,
            "AI");

        RegisterDefaultFaction(
            AncientsFactionId,
            "Ancients");

        RegisterDefaultFaction(
            InfectedFactionId,
            "Infected");
    }

    private GalaxyRuntimeState GetGalaxyState()
    {
        GameRuntimeState gameState =
            _gameSessionService != null
                ? _gameSessionService.State
                : null;

        return gameState != null
            ? gameState.Galaxy
            : null;
    }

    private void RegisterDefaultFaction(
        string factionId,
        string displayName)
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return;

        string normalizedFactionId =
            NormalizeFactionId(factionId);

        EnemyFactionState existingFaction =
            galaxyState.EnemyFactions.FirstOrDefault(
                faction =>
                    faction != null &&
                    NormalizeFactionId(faction.FactionId) == normalizedFactionId);

        if (existingFaction != null)
        {
            if (string.IsNullOrWhiteSpace(existingFaction.DisplayName))
                existingFaction.DisplayName = displayName;

            EnsureFactionLists(existingFaction);
            return;
        }

        galaxyState.EnemyFactions.Add(
            new EnemyFactionState
            {
                FactionId = normalizedFactionId,
                DisplayName = displayName,
                OwnedSystemIds = new List<string>(),
                TerritorySystemIds = new List<string>(),
                ActiveGroupRuntimeIds = new List<string>(),
                ActiveInvasionIds = new List<string>()
            });
    }

    private static void RepairFactionList(
        GalaxyRuntimeState galaxyState)
    {
        if (galaxyState.EnemyFactions == null)
            galaxyState.EnemyFactions = new List<EnemyFactionState>();

        for (int i = galaxyState.EnemyFactions.Count - 1; i >= 0; i--)
        {
            EnemyFactionState factionState =
                galaxyState.EnemyFactions[i];

            if (factionState == null ||
                string.IsNullOrWhiteSpace(factionState.FactionId))
            {
                galaxyState.EnemyFactions.RemoveAt(i);
                continue;
            }

            factionState.FactionId =
                NormalizeFactionId(factionState.FactionId);

            EnsureFactionLists(factionState);
        }
    }

    private static void EnsureFactionLists(
        EnemyFactionState factionState)
    {
        factionState.OwnedSystemIds ??= new List<string>();
        factionState.TerritorySystemIds ??= new List<string>();
        factionState.ActiveGroupRuntimeIds ??= new List<string>();
        factionState.ActiveInvasionIds ??= new List<string>();
    }

    private static string NormalizeFactionId(
        string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId)
            ? string.Empty
            : factionId.Trim().ToLowerInvariant();
    }

    private static bool ContainsId(
        List<string> ids,
        string id)
    {
        if (ids == null ||
            string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return ids.Contains(id);
    }

    private static void AddUniqueId(
        List<string> ids,
        string id)
    {
        if (ids == null ||
            string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!ids.Contains(id))
            ids.Add(id);
    }

    private static void RemoveId(
        List<string> ids,
        string id)
    {
        if (ids == null ||
            string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        ids.Remove(id);
    }
}