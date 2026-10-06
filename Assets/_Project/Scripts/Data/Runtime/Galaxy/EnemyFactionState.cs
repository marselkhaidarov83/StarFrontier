using System;
using System.Collections.Generic;

[Serializable]
public sealed class EnemyFactionState
{
    public string FactionId;
    public string DisplayName;

    public List<string> OwnedSystemIds = new();
    public List<string> TerritorySystemIds = new();
    public List<string> ActiveGroupRuntimeIds = new();
    public List<string> ActiveInvasionIds = new();

    public bool HasOwnershipLink(string systemId)
    {
        return !string.IsNullOrWhiteSpace(systemId) &&
               OwnedSystemIds != null &&
               OwnedSystemIds.Contains(systemId);
    }
}