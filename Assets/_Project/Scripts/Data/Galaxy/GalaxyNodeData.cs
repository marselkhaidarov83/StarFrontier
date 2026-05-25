using System;
using System.Collections.Generic;

    [Serializable]
    public sealed class GalaxyNodeData
    {
        public string SystemId { get; }
        public string DisplayName { get; }
        public int DangerLevel { get; }
        public SystemEconomyType EconomyType { get; }

        // Храним как IReadOnlyList, чтобы снаружи никто не менял список случайно.
        public IReadOnlyList<string> NeighborIds => _neighborIds;
        private readonly List<string> _neighborIds;

        public GalaxyNodeData(
            string systemId,
            string displayName,
            int dangerLevel,
            SystemEconomyType economyType,
            // IEnumerable<string> neighborSystemIds,
            IEnumerable<StarSystemLink> linkedSystems)
        {
            if (string.IsNullOrWhiteSpace(systemId))
                throw new ArgumentException("GalaxyNodeData: systemId cannot be null or empty.", nameof(systemId));

            SystemId = systemId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SystemId : displayName.Trim();
            DangerLevel = Math.Max(0, dangerLevel);
            EconomyType = economyType;

            _neighborIds = new List<string>();

            // if (neighborSystemIds == null)
            //     return;
            if (linkedSystems == null)
                return;

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);

            // foreach (var neighborId in neighborSystemIds)
            foreach (var linkedSystem in linkedSystems)
            {
                if (string.IsNullOrWhiteSpace(linkedSystem.LinkedSystem.Id))
                    continue;
                // if (string.IsNullOrWhiteSpace(neighborId))
                //     continue;

                // var normalizedId = neighborId.Trim();
                var normalizedId = linkedSystem.LinkedSystem.Id.Trim();

                // Не даем системе ссылаться самой на себя.
                if (string.Equals(normalizedId, SystemId, StringComparison.Ordinal))
                    continue;

                // Убираем дубликаты соседей.
                if (uniqueIds.Add(normalizedId))
                    _neighborIds.Add(normalizedId);
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
            return $"GalaxyNodeData(Id={SystemId}, Name={DisplayName}, Danger={DangerLevel}, Economy={EconomyType}, Neighbors={_neighborIds.Count})";
        }
    }