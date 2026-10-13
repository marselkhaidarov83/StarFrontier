using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class EnemyFactionState
{
    public string FactionId;
    public string DisplayName;

    public List<string> OwnedSystemIds = new();
    public List<string> TerritorySystemIds = new();
    public List<string> FrontlineSystemIds = new();
    public List<string> FrontierSystemIds = new();
    public List<string> ActiveGroupRuntimeIds = new();
    public List<string> ActiveInvasionIds = new();

    public void EnsureWarTerritoryLists()
    {
        OwnedSystemIds ??= new List<string>();
        TerritorySystemIds ??= new List<string>();
        FrontlineSystemIds ??= new List<string>();
        FrontierSystemIds ??= new List<string>();
        ActiveGroupRuntimeIds ??= new List<string>();
        ActiveInvasionIds ??= new List<string>();
    }

    public bool HasOwnershipLink(string systemId)
    {
        EnsureWarTerritoryLists();

        return !string.IsNullOrWhiteSpace(systemId) &&
               OwnedSystemIds.Contains(systemId);
    }

    public bool HasTerritoryLink(string systemId)
    {
        EnsureWarTerritoryLists();

        return !string.IsNullOrWhiteSpace(systemId) &&
               TerritorySystemIds.Contains(systemId);
    }

    public bool IsFrontlineSystem(string systemId)
    {
        EnsureWarTerritoryLists();

        return !string.IsNullOrWhiteSpace(systemId) &&
               FrontlineSystemIds.Contains(systemId);
    }

    public bool IsFrontierSystem(string systemId)
    {
        EnsureWarTerritoryLists();

        return !string.IsNullOrWhiteSpace(systemId) &&
               FrontierSystemIds.Contains(systemId);
    }

    public void RebuildFrontlineAndFrontier(
        IReadOnlyDictionary<string, IReadOnlyList<string>> neighborSystemIdsBySystemId)
    {
        EnsureWarTerritoryLists();

        FrontlineSystemIds.Clear();
        FrontierSystemIds.Clear();

        if (neighborSystemIdsBySystemId == null)
            return;

        HashSet<string> ownedSystems =
            new HashSet<string>(
                OwnedSystemIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        HashSet<string> territorySystems =
            new HashSet<string>(
                TerritorySystemIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        foreach (string ownedSystemId in ownedSystems)
        {
            if (!neighborSystemIdsBySystemId.TryGetValue(
                    ownedSystemId,
                    out IReadOnlyList<string> neighbors))
            {
                continue;
            }

            bool hasExternalNeighbor = false;

            for (int i = 0; i < neighbors.Count; i++)
            {
                string neighborSystemId =
                    neighbors[i];

                if (string.IsNullOrWhiteSpace(neighborSystemId))
                    continue;

                if (ownedSystems.Contains(neighborSystemId))
                    continue;

                hasExternalNeighbor = true;

                if (!FrontierSystemIds.Contains(neighborSystemId))
                    FrontierSystemIds.Add(neighborSystemId);
            }

            if (hasExternalNeighbor &&
                !FrontlineSystemIds.Contains(ownedSystemId))
            {
                FrontlineSystemIds.Add(ownedSystemId);
            }
        }

        foreach (string territorySystemId in territorySystems)
        {
            if (ownedSystems.Contains(territorySystemId))
                continue;

            if (!FrontierSystemIds.Contains(territorySystemId))
                FrontierSystemIds.Add(territorySystemId);
        }
    }
}