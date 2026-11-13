using System.Collections.Generic;

public class CurrentMarketItemsProvider : ICurrentMarketItemsProvider
{
    private readonly IPlanetMarketService _market;
    public CurrentMarketItemsProvider() { _market = Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMarketService>(); }
    public CurrentMarketItemsProvider(IConfigService config, IGameSessionService session)
    { _market = new PlanetMarketService(session, config, null, null); }
    public IReadOnlyList<MarketItemEntry> GetAvailableItems() => _market.GetItems();
}
