using System;
using System.Collections.Generic;
using UnityEngine;


public class EconomyService : CustomService, IEconomyService
{
    private readonly SimpleEventBus eventBus;
    private readonly IConfigService _configService;
    private readonly IGameSessionService _gameSessionService;
    private readonly IInventoryService _inventoryService;
    private IItemPriceProvider _itemPriceProvider;

    public EconomyService()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _inventoryService = Bootstrapper.Instance.ServiceRegistry.Get<IInventoryService>();
        _itemPriceProvider = new PlanetMarketPriceProvider();
    }

    public int GetBuyPrice(string itemId)
    {
        if (!_itemPriceProvider.TryGetBuyPrice(itemId, out int price))
            price = 999;

        if (IsDebug())
            AppLog.Info(DebugLogChannel.Economy,
                typeof(EconomyService),
                "GetBuyPrice = " + price);

        return price;
    }

    public int GetSellPrice(string itemId)
    {
        if (!_itemPriceProvider.TryGetSellPrice(itemId, out int price))
            price = 0;

        if (IsDebug())
            AppLog.Info(DebugLogChannel.Economy,
                typeof(EconomyService),
                "GetBuyPrice = " + price);

        return price;
    }

    public List<MarketItemData> GetMarketItemsForCurrentSystem()
    {
        List<MarketItemData> result = new List<MarketItemData>();
        IReadOnlyList<ItemConfig> allItems = _configService.GetAllItems();

        AppLog.Info(DebugLogChannel.Economy, "EconomyService | allItems count: " + allItems.Count);

        for (int i = 0; i < allItems.Count; i++)
        {
            ItemConfig item = allItems[i];

            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            int buyPrice = GetBuyPrice(item.Id);
            int sellPrice = GetSellPrice(item.Id);

            result.Add(new MarketItemData(
                item.Id,
                item.DisplayName,
                item.BasePrice,
                buyPrice,
                sellPrice,
                item
            ));
        }

        return result;
    }

    public int GetCredits()
    {
        return _gameSessionService.State.Player.Credits;
    }

    public bool CanBuy(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (quantity <= 0)
            return false;

        int buyPrice = GetBuyPrice(itemId);
        if (buyPrice <= 0)
            return false;

        int totalPrice = buyPrice * quantity;

        if (_gameSessionService.State.Player.Credits < totalPrice)
            return false;

        if (!_inventoryService.CanAddItem(itemId, quantity))
            return false;

        return true;
    }

    public bool CanSell(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (quantity <= 0)
            return false;

        int sellPrice = GetSellPrice(itemId);
        if (sellPrice <= 0)
            return false;

        int ownedCount = _inventoryService.GetItemCount(itemId);
        return ownedCount >= quantity;
    }

    public EconomyOperationResult BuyItem(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return EconomyOperationResult.Failed(EconomyErrorCode.InvalidItemId, itemId, quantity);

        if (quantity <= 0)
            return EconomyOperationResult.Failed(EconomyErrorCode.InvalidQuantity, itemId, quantity);

        int price = GetBuyPrice(itemId);
        if (price <= 0)
            return EconomyOperationResult.Failed(EconomyErrorCode.PriceNotFound, itemId, quantity);

        int totalPrice = price * quantity;

        if (_gameSessionService.State.Player.Credits < totalPrice)
            return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughCredits, itemId, quantity);

        if (!_inventoryService.CanAddItem(itemId, quantity))
            return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughCargoSpace, itemId, quantity);

        bool addSucceeded = _inventoryService.AddItem(itemId, quantity);
        if (!addSucceeded)
            return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughCargoSpace, itemId, quantity);

        _gameSessionService.State.Player.Credits -= totalPrice;
        PublishCreditsChanged();

        eventBus.Publish(new SaveNeedEvent());
        return EconomyOperationResult.Completed(-totalPrice, itemId, quantity);
    }

    public EconomyOperationResult SellItem(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return EconomyOperationResult.Failed(EconomyErrorCode.InvalidItemId, itemId, quantity);

        if (quantity <= 0)
            return EconomyOperationResult.Failed(EconomyErrorCode.InvalidQuantity, itemId, quantity);

        int sellPrice = GetSellPrice(itemId);
        // if (!_itemPriceProvider.TryGetSellPrice(itemId, out int price))
        if (sellPrice <= 0)
            return EconomyOperationResult.Failed(EconomyErrorCode.PriceNotFound, itemId, quantity);

        int ownedCount = _inventoryService.GetItemCount(itemId);
        if (ownedCount <= 0)
            return EconomyOperationResult.Failed(EconomyErrorCode.ItemNotOwned, itemId, quantity);

        if (ownedCount < quantity)
            return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughItemsToSell, itemId, quantity);

        bool removeSucceeded = _inventoryService.RemoveItem(itemId, quantity);
        if (!removeSucceeded)
            return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughItemsToSell, itemId, quantity);

        int totalPrice = sellPrice * quantity;
        _gameSessionService.State.Player.Credits += totalPrice;
        PublishCreditsChanged();

        eventBus.Publish(new SaveNeedEvent());
        return EconomyOperationResult.Completed(totalPrice, itemId, quantity);
    }

    public void AddCredits(int amount)
    {
        if (amount <= 0)
            return;

        _gameSessionService.State.Player.Credits += amount;
        PublishCreditsChanged();
    }

    public bool SpendCredits(int amount)
    {
        if (amount <= 0)
            return false;

        if (_gameSessionService.State.Player.Credits < amount)
            return false;

        _gameSessionService.State.Player.Credits -= amount;
        PublishCreditsChanged();
        return true;
    }

    private void PublishCreditsChanged()
    {
        eventBus.Publish(new CreditsChangedEvent(_gameSessionService.State.Player.Credits));
    }
}