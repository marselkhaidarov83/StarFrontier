using System;
using System.Collections.Generic;
using UnityEngine;

    public sealed class GalaxyGraphModel
    {
        private readonly Dictionary<string, GalaxyNodeData> _systemsById;

        public GalaxyGraphModel(IEnumerable<GalaxyNodeData> systems)
        {
            if (systems == null)
                throw new ArgumentNullException(nameof(systems));

            _systemsById = new Dictionary<string, GalaxyNodeData>(StringComparer.Ordinal);

            BuildIndex(systems);
            ValidateNeighborLinks();
        }

        public IReadOnlyCollection<GalaxyNodeData> GetAllSystems()
        {
            return _systemsById.Values;
        }

        public GalaxyNodeData GetSystem(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId))
                throw new ArgumentException("GalaxyGraphModel: systemId cannot be null or empty.", nameof(systemId));

            var normalizedId = NormalizeId(systemId);

            if (_systemsById.TryGetValue(normalizedId, out var system))
                return system;

            throw new KeyNotFoundException($"GalaxyGraphModel: system '{normalizedId}' was not found.");
        }

        public bool TryGetSystem(string systemId, out GalaxyNodeData system)
        {
            system = null;

            if (string.IsNullOrWhiteSpace(systemId))
                return false;

            var normalizedId = NormalizeId(systemId);
            return _systemsById.TryGetValue(normalizedId, out system);
        }

        public IReadOnlyList<GalaxyNodeData> GetNeighbors(string systemId)
        {
            if (!TryGetSystem(systemId, out var system))
            {
                Debug.LogWarning($"GalaxyGraphModel.GetNeighbors: system '{systemId}' was not found.");
                return Array.Empty<GalaxyNodeData>();
            }

            var neighbors = new List<GalaxyNodeData>();

            foreach (var neighborId in system.NeighborIds)
            {
                if (_systemsById.TryGetValue(neighborId, out var neighbor))
                {
                    neighbors.Add(neighbor);
                }
                else
                {
                    Debug.LogWarning(
                        $"GalaxyGraphModel.GetNeighbors: system '{system.SystemId}' references missing neighbor '{neighborId}'.");
                }
            }

            return neighbors;
        }

        public bool AreNeighbors(string fromSystemId, string toSystemId)
        {
            if (!TryGetSystem(fromSystemId, out var fromSystem))
                return false;

            if (string.IsNullOrWhiteSpace(toSystemId))
                return false;

            var normalizedToId = NormalizeId(toSystemId);
            return fromSystem.HasNeighbor(normalizedToId);
        }

        public bool ContainsSystem(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId))
                return false;

            return _systemsById.ContainsKey(NormalizeId(systemId));
        }

        private void BuildIndex(IEnumerable<GalaxyNodeData> systems)
        {
            foreach (var system in systems)
            {
                if (system == null)
                {
                    Debug.LogWarning("GalaxyGraphModel.BuildIndex: null system entry was skipped.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(system.SystemId))
                {
                    Debug.LogWarning("GalaxyGraphModel.BuildIndex: system with empty id was skipped.");
                    continue;
                }

                var normalizedId = NormalizeId(system.SystemId);

                if (_systemsById.ContainsKey(normalizedId))
                {
                    Debug.LogWarning($"GalaxyGraphModel.BuildIndex: duplicate system id '{normalizedId}' was skipped.");
                    continue;
                }

                _systemsById.Add(normalizedId, system);
            }
        }

        private void ValidateNeighborLinks()
        {
            foreach (var pair in _systemsById)
            {
                var system = pair.Value;

                foreach (var neighborId in system.NeighborIds)
                {
                    if (!_systemsById.ContainsKey(neighborId))
                    {
                        Debug.LogWarning(
                            $"GalaxyGraphModel.ValidateNeighborLinks: system '{system.SystemId}' has missing neighbor '{neighborId}'.");
                    }
                }
            }
        }

        private static string NormalizeId(string systemId)
        {
            return systemId.Trim();
        }

        
    }