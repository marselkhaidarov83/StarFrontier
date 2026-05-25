using System;
using System.Collections.Generic;

    /// <summary>
    /// Создает GalaxyGraphModel из списка StarSystemConfig.
    /// Это отдельный класс, чтобы не смешивать:
    /// - данные конфигов
    /// - доменную модель карты
    /// </summary>
    public static class GalaxyGraphFactory
    {
        public static GalaxyGraphModel CreateFromConfigs(
            IEnumerable<StarSystemConfig> systemConfigs)
        {
            if (systemConfigs == null)
                throw new ArgumentNullException(nameof(systemConfigs));

            var nodes = new List<GalaxyNodeData>();

            foreach (var config in systemConfigs)
            {
                if (config == null)
                    continue;

                var node = CreateNodeFromConfig(config);

                if (node != null)
                    nodes.Add(node);
            }

            return new GalaxyGraphModel(nodes);
        }

        private static GalaxyNodeData CreateNodeFromConfig(
            StarSystemConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                UnityEngine.Debug.LogWarning(
                    "GalaxyGraphFactory: StarSystemConfig has empty Id.");
                return null;
            }

            return new GalaxyNodeData(
                systemId: config.Id,
                displayName: config.DisplayName,
                dangerLevel: config.DangerLevel,
                economyType: config.EconomyType,
                // neighborSystemIds: config.NeighborSystemIds,
                linkedSystems: config.LinkedSystems
            );
        }
    }