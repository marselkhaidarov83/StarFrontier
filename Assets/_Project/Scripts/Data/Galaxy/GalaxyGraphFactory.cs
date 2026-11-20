using System;
using System.Collections.Generic;

public static class GalaxyGraphFactory
{
    public static GalaxyGraphModel CreateFromConfigs(
        IEnumerable<StarSystemConfig> systemConfigs, GalaxyRuntimeState runtimeState)
    {
        if (systemConfigs == null) throw new ArgumentNullException(nameof(systemConfigs));
        var nodes = new List<GalaxyNodeData>();
        foreach (StarSystemConfig config in systemConfigs)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.Id)) continue;
            var neighbors = new List<string>();
            if (config.Routes != null)
                foreach (RouteConfig route in config.Routes)
                {
                    StarSystemConfig other = route?.GetOtherSystem(config.Id);
                    if (other != null && !string.IsNullOrWhiteSpace(other.Id) && route.IsUnlocked(runtimeState))
                        neighbors.Add(other.Id);
                }
            nodes.Add(new GalaxyNodeData(config.Id, config.DisplayName, neighbors));
        }
        return new GalaxyGraphModel(nodes);
    }
}
