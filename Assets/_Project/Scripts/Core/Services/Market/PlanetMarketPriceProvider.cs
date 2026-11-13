public class PlanetMarketPriceProvider : IItemPriceProvider
{
    private readonly IPlanetMarketService _market;
    public PlanetMarketPriceProvider() : this(Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMarketService>()) { }
    public PlanetMarketPriceProvider(IPlanetMarketService market) { _market = market; }
    public bool TryGetBuyPrice(string itemId, out int price) { price = _market.GetPrice(itemId, true); return price > 0; }
    public bool TryGetSellPrice(string itemId, out int price) { price = _market.GetPrice(itemId, false); return price > 0; }
}
