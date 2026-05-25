using System.Linq;

    public class PlanetMarketPriceProvider : IItemPriceProvider
    {
        private readonly IConfigService configService;
        private readonly IGameSessionService gameSessionService;
        // private readonly ICurrentPlanetProvider _currentPlanetProvider;
        // private readonly IItemBasePriceProvider _itemBasePriceProvider;

        public PlanetMarketPriceProvider()
            // ICurrentPlanetProvider currentPlanetProvider,
            // IItemBasePriceProvider itemBasePriceProvider)
        {
            configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
            gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
            // _currentPlanetProvider = currentPlanetProvider;
            // _itemBasePriceProvider = itemBasePriceProvider;
        }

        public bool TryGetBuyPrice(string itemId, out int price)
        {
            price = 0;

            if (!TryGetMarketEntry(itemId, out MarketItemEntry entry))
                return false;

            int basePrice = configService.GetItemConfigById(itemId).BasePrice;
            // if (!_itemBasePriceProvider.TryGetBasePrice(itemId, out int basePrice))
            //     return false;

            price = UnityEngine.Mathf.RoundToInt(basePrice * entry.BuyMultiplier);
            return true;
        }

        public bool TryGetSellPrice(string itemId, out int price)
        {
            price = 0;

            if (!TryGetMarketEntry(itemId, out MarketItemEntry entry))
                return false;

            int basePrice = configService.GetItemConfigById(itemId).BasePrice;
            // if (!_itemBasePriceProvider.TryGetBasePrice(itemId, out int basePrice))
            //     return false;

            price = UnityEngine.Mathf.RoundToInt(basePrice * entry.SellMultiplier);
            return true;
        }

        private bool TryGetMarketEntry(string itemId, out MarketItemEntry entry)
        {
            entry = null;

            PlanetConfig currentPlanet = 
                    configService.GetPlanetConfigById(gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId);
            // PlanetConfig currentPlanet = _currentPlanetProvider.GetCurrentPlanet();
            
            if (currentPlanet == null || currentPlanet.MarketProfile == null)
                return false;

            entry = currentPlanet.MarketProfile.Items
                .FirstOrDefault(x => x != null && x.ItemConfig.Id == itemId && x.IsAvailable);

            return entry != null;
        }
    }
