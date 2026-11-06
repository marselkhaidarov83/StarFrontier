using System;
using System.Collections.Generic;
using System.Linq;

public class CurrentMarketItemsProvider : ICurrentMarketItemsProvider
{
    private readonly IConfigService _config;
    private readonly IGameSessionService _session;
    public CurrentMarketItemsProvider() : this(
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>()) { }
    public CurrentMarketItemsProvider(IConfigService config, IGameSessionService session)
    { _config = config; _session = session; }

    public IReadOnlyList<MarketItemEntry> GetAvailableItems()
    {
        var system = _config.GetCurrentSystemConfig();
        var profile = !string.IsNullOrEmpty(_session?.State?.Player?.CurrentStationId)
            ? StationLocationRules.Market(_session.State, system)
            : _config.GetCurrentPlanetConfig()?.MarketProfile;
        return profile?.Items?.Where(x => x != null && x.IsAvailable).ToList()
            ?? new List<MarketItemEntry>();
    }
}
