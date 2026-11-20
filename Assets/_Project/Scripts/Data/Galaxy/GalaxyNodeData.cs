using System;
using System.Collections.Generic;

[Serializable]
public sealed class GalaxyNodeData
{
    public string SystemId { get; }
    public string DisplayName { get; }

    // Храним как IReadOnlyList, чтобы снаружи никто не менял список случайно.
    public IReadOnlyList<string> NeighborIds => _neighborIds;
    private readonly List<string> _neighborIds;

    public GalaxyNodeData(string systemId, string displayName, IEnumerable<string> neighborSystemIds)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            throw new ArgumentException("GalaxyNodeData: systemId cannot be null or empty.", nameof(systemId));

        SystemId = systemId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? SystemId : displayName.Trim();

        _neighborIds = new List<string>();
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        if (neighborSystemIds == null) return;
        foreach (string neighborId in neighborSystemIds)
        {
            if (string.IsNullOrWhiteSpace(neighborId)) continue;
            string id = neighborId.Trim();
            if (id != SystemId && uniqueIds.Add(id)) _neighborIds.Add(id);
        }
    }

    public bool HasNeighbor(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        var normalizedId = systemId.Trim();

        for (int i = 0; i < _neighborIds.Count; i++)
        {
            if (string.Equals(_neighborIds[i], normalizedId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public override string ToString()
    {
        return $"GalaxyNodeData(Id={SystemId}, Name={DisplayName}, Neighbors={_neighborIds.Count})";
    }
}
