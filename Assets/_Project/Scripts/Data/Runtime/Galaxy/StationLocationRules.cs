using System;
using System.Linq;
public static class StationLocationRules
{
    public static bool IsInside(GameRuntimeState state, StarSystemConfig system)
    {
        var player = state?.Player;
        var saved = state?.Galaxy?.Systems?.Find(x => x != null && x.SystemId == player?.CurrentSystemId);
        return player != null && player.GetActiveShip() != null && player.GetActiveShip().CurrentHull > 0 && system != null && player.CurrentSystemId == system.Id && system.Station != null &&
            player.CurrentStationId == system.Station.Id && string.IsNullOrEmpty(player.CurrentPlanetId) &&
            saved?.Station != null && saved.Station.ConfigId == player.CurrentStationId && saved.Station.CanDock && !saved.Station.ConfigMissing &&
            saved.SystemStatus != StarSystemStatus.Captured;
    }
    public static MarketProfileConfig Market(GameRuntimeState state, StarSystemConfig system)
    {
        if (!IsInside(state, system)) return null;
        if (system.Station.MarketProfile != null) return system.Station.MarketProfile;
        // Basic station services use a local inhabited market when no station override is configured.
        return system.PlanetRefs?.FirstOrDefault(p => p != null && p.IsInhabited && p.MarketProfile != null)?.MarketProfile;
    }
}
