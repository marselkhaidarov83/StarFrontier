using System;
using System.Collections.Generic;
using System.Linq;

public class EconomyService : CustomService, IEconomyService
{
    private readonly SimpleEventBus _events;
    private readonly IGameSessionService _session;
    private readonly IPlanetMarketService _market;
    public EconomyService() : this(Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMarketService>(), Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>()) { }
    public EconomyService(IGameSessionService session, IPlanetMarketService market, SimpleEventBus events)
    { _session = session; _market = market; _events = events; }
    public int GetBuyPrice(string itemId) => _market.GetPrice(itemId, true);
    public int GetSellPrice(string itemId) => _market.GetPrice(itemId, false);
    public List<MarketItemData> GetMarketItemsForCurrentSystem() => _market.GetItems().Select(x => new MarketItemData(
        x.ItemConfig.Id, x.ItemConfig.DisplayName, x.ItemConfig.BasePrice, GetBuyPrice(x.ItemConfig.Id), GetSellPrice(x.ItemConfig.Id), x.ItemConfig)).ToList();
    public int GetCredits() => _session.State?.Player?.Credits ?? 0;
    public bool CanBuy(string itemId, int quantity) => _market.CanTrade(itemId, quantity, true);
    public bool CanSell(string itemId, int quantity) => _market.CanTrade(itemId, quantity, false);
    public EconomyOperationResult BuyItem(string itemId, int quantity) => Trade(itemId, quantity, true);
    public EconomyOperationResult SellItem(string itemId, int quantity) => Trade(itemId, quantity, false);
    private EconomyOperationResult Trade(string itemId, int quantity, bool buy)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(buy ? PlayerAnalyticsAction.BuyItem : PlayerAnalyticsAction.SellItem, itemId, quantity))
        {
            var result = _market.Trade(itemId, quantity, buy);
            observation.Complete(result.Success, result.ErrorCode.ToString(), (int)Math.Min(int.MaxValue, Math.Abs((long)result.ChangedCredits)));
            return result;
        }
    }
    public void AddCredits(int amount)
    {
        if (amount <= 0 || _session.State?.Player == null || (long)GetCredits() + amount > int.MaxValue) return;
        _session.State.Player.Credits += amount; Changed();
    }
    public bool SpendCredits(int amount)
    {
        if (amount <= 0 || GetCredits() < amount) return false;
        _session.State.Player.Credits -= amount; Changed(); return true;
    }
    private void Changed() => _events?.PublishSafely(new CreditsChangedEvent(GetCredits()), _ => { });
}
