using System.Collections.Generic;
using System.Linq;

    public class CurrentMarketItemsProvider : ICurrentMarketItemsProvider
    {
        private IConfigService configService;

        public CurrentMarketItemsProvider()
        {
            configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        }

        public IReadOnlyList<MarketItemEntry> GetAvailableItems()
        {
            var planet = configService.GetCurrentPlanetConfig();

            if (planet == null || planet.MarketProfile == null || planet.MarketProfile.Items == null)
                return new List<MarketItemEntry>();

            return planet.MarketProfile.Items
                .Where(x => x != null && x.IsAvailable)
                .ToList();
        }
    }