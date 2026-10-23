public class MarketTransactionService : IMarketTransactionService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IConfigService _configService;
    private readonly IEconomyService _economyService;
    private readonly IInventoryService _inventoryService;

    public MarketTransactionService()
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _economyService = Bootstrapper.Instance.ServiceRegistry.Get<IEconomyService>();
        _inventoryService = Bootstrapper.Instance.ServiceRegistry.Get<IInventoryService>();
    }

    public BuyItemResult BuyItem(string itemId, int quantity)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.BuyItem, itemId, quantity))
        {
            var result = BuyItemCore(itemId, quantity);
            observation.Complete(result.IsSuccess, result.resultType.ToString(), result.totalPrice);
            return result;
        }
    }

    private BuyItemResult BuyItemCore(string itemId, int quantity)
    {
        PlayerState playerProfile = _gameSessionService.State.Player;
        if (playerProfile == null)
        {
            return BuyItemResult.Create(
                BuyItemResultType.MissingPlayerProfile,
                itemId,
                quantity,
                0,
                0);
        }

        RuntimeCargoInventory cargoInventory = playerProfile.PlayerShipState.GetActiveShip().Cargo;
        if (cargoInventory == null)
        {
            return BuyItemResult.Create(
                BuyItemResultType.MissingCargoInventory,
                itemId,
                quantity,
                0,
                0);
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return BuyItemResult.Create(
                BuyItemResultType.InvalidItem,
                itemId,
                quantity,
                0,
                0);
        }

        if (quantity <= 0)
        {
            return BuyItemResult.Create(
                BuyItemResultType.InvalidQuantity,
                itemId,
                quantity,
                0,
                0);
        }

        ItemConfig itemConfig = _configService.GetItemConfigById(itemId);
        if (itemConfig == null)
        {
            return BuyItemResult.Create(
                BuyItemResultType.InvalidItem,
                itemId,
                quantity,
                0,
                0);
        }

        int unitPrice = _economyService.GetBuyPrice(itemId);
        if (unitPrice <= 0)
        {
            return BuyItemResult.Create(
                BuyItemResultType.InvalidPrice,
                itemId,
                quantity,
                unitPrice,
                0);
        }

        int totalPrice = unitPrice * quantity;

        if (playerProfile.Credits < totalPrice)
        {
            return BuyItemResult.Create(
                BuyItemResultType.NotEnoughCredits,
                itemId,
                quantity,
                unitPrice,
                totalPrice);
        }

        int maxCargoCapacity = playerProfile.PlayerShipState.GetActiveShip().CargoCapacity;

        if (!_inventoryService.CanAddItem(itemId, quantity))
        {
            return BuyItemResult.Create(
                BuyItemResultType.NotEnoughCargoSpace,
                itemId,
                quantity,
                unitPrice,
                totalPrice);
        }

        int creditsBefore = playerProfile.Credits;
        int itemQuantityBefore = cargoInventory.GetQuantity(itemId);

        bool cargoAdded = cargoInventory.AddItem(itemId, quantity, maxCargoCapacity);
        if (!cargoAdded)
        {
            return BuyItemResult.Create(
                BuyItemResultType.NotEnoughCargoSpace,
                itemId,
                quantity,
                unitPrice,
                totalPrice);
        }

        playerProfile.Credits -= totalPrice;

        if (playerProfile.Credits < 0)
        {
            playerProfile.Credits = creditsBefore;

            int currentQuantity = cargoInventory.GetQuantity(itemId);
            int rollbackAmount = currentQuantity - itemQuantityBefore;

            if (rollbackAmount > 0)
            {
                cargoInventory.RemoveItem(itemId, rollbackAmount);
            }

            return BuyItemResult.Create(
                BuyItemResultType.NotEnoughCredits,
                itemId,
                quantity,
                unitPrice,
                totalPrice);
        }

        return BuyItemResult.Create(
            BuyItemResultType.Success,
            itemId,
            quantity,
            unitPrice,
            totalPrice);
    }

    public SellItemResult SellItem(string itemId, int quantity)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.SellItem, itemId, quantity))
        {
            var result = SellItemCore(itemId, quantity);
            observation.Complete(result.IsSuccess, result.resultType.ToString(), result.totalPrice);
            return result;
        }
    }

    private SellItemResult SellItemCore(string itemId, int quantity)
    {
        PlayerState playerProfile = _gameSessionService.State.Player;
        if (playerProfile == null)
        {
            return SellItemResult.Create(
                SellItemResultType.MissingPlayerProfile,
                itemId,
                quantity,
                0,
                0);
        }

        RuntimeCargoInventory cargoInventory = playerProfile.PlayerShipState.GetActiveShip().Cargo;
        if (cargoInventory == null)
        {
            return SellItemResult.Create(
                SellItemResultType.MissingCargoInventory,
                itemId,
                quantity,
                0,
                0);
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return SellItemResult.Create(
                SellItemResultType.InvalidItem,
                itemId,
                quantity,
                0,
                0);
        }

        if (quantity <= 0)
        {
            return SellItemResult.Create(
                SellItemResultType.InvalidQuantity,
                itemId,
                quantity,
                0,
                0);
        }

        ItemConfig itemConfig = _configService.GetItemConfigById(itemId);
        if (itemConfig == null)
        {
            return SellItemResult.Create(
                SellItemResultType.InvalidItem,
                itemId,
                quantity,
                0,
                0);
        }

        int ownedQuantity = cargoInventory.GetTradeableQuantity(itemId);

        if (ownedQuantity <= 0)
        {
            return SellItemResult.Create(
                SellItemResultType.ItemNotOwned,
                itemId,
                quantity,
                0,
                0);
        }

        if (ownedQuantity < quantity)
        {
            return SellItemResult.Create(
                SellItemResultType.NotEnoughQuantity,
                itemId,
                quantity,
                0,
                0);
        }

        int unitPrice = _economyService.GetSellPrice(itemId);
        if (unitPrice <= 0)
        {
            return SellItemResult.Create(
                SellItemResultType.InvalidPrice,
                itemId,
                quantity,
                unitPrice,
                0);
        }

        int totalPrice = unitPrice * quantity;

        int creditsBefore = playerProfile.Credits;
        int itemQuantityBefore = cargoInventory.GetQuantity(itemId);

        bool removed = cargoInventory.RemoveItem(itemId, quantity);
        if (!removed)
        {
            return SellItemResult.Create(
                SellItemResultType.NotEnoughQuantity,
                itemId,
                quantity,
                unitPrice,
                totalPrice);
        }

        playerProfile.Credits += totalPrice;

        if (cargoInventory.GetQuantity(itemId) < 0)
        {
            playerProfile.Credits = creditsBefore;

            int currentQuantity = cargoInventory.GetQuantity(itemId);
            int rollbackAmount = itemQuantityBefore - currentQuantity;

            if (rollbackAmount > 0)
            {
                cargoInventory.AddItem(itemId, rollbackAmount, playerProfile.PlayerShipState.GetActiveShip().CargoCapacity);
            }

            return SellItemResult.Create(
                SellItemResultType.NotEnoughQuantity,
                itemId,
                quantity,
                unitPrice,
                totalPrice);
        }

        return SellItemResult.Create(
            SellItemResultType.Success,
            itemId,
            quantity,
            unitPrice,
            totalPrice);
    }
}