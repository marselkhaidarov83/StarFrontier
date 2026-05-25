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
        // _priceCalculator = new PriceCalculator();
        _itemPriceProvider = new PlanetMarketPriceProvider();
    }

    public int GetBuyPrice(string itemId)
    {
        if (!_itemPriceProvider.TryGetBuyPrice(itemId, out int price))
            price = 999;

        if (IsDebug())
            Debug.Log("[EconomyService] GetBuyPrice = " + price);

        return price;

        // ItemConfig itemConfig = _configService.GetItemConfigById(itemId);
        // if (itemConfig == null)
        //     return 0;

        //     StarSystemConfig systemConfig = GetCurrentSystemConfig();

        //     float economyModifier = 1f;

        //     if (systemConfig != null)
        //     {
        //         economyModifier = GetEconomyModifier(systemConfig.EconomyType, itemConfig.ItemCategory);
        //     }

        //     PriceCalculationInput input = new PriceCalculationInput
        //     {
        //         basePrice = itemConfig.BasePrice,
        //         economyModifier = economyModifier,
        //         scarcityModifier = 1f,
        //         buyCoefficient = 1f,
        //         sellCoefficient = 0.7f
        //     };

        //     return _priceCalculator.CalculateBuyPrice(input);
    }

    public int GetSellPrice(string itemId)
    {
        if (!_itemPriceProvider.TryGetSellPrice(itemId, out int price))
            price = 0;
        
        if (IsDebug())
            Debug.Log("[EconomyService] GetBuyPrice = " + price);

        return price;


            // ItemConfig itemConfig = _configService.GetItemConfigById(itemId);
            // if (itemConfig == null)
            // {
            //     return 0;
            // }

            // StarSystemConfig systemConfig = GetCurrentSystemConfig();

            // float economyModifier = 1f;

            // if (systemConfig != null)
            // {
            //     economyModifier = GetEconomyModifier(systemConfig.EconomyType, itemConfig.ItemCategory);
            // }

            // PriceCalculationInput input = new PriceCalculationInput
            // {
            //     basePrice = itemConfig.BasePrice,
            //     economyModifier = economyModifier,
            //     scarcityModifier = 1f,
            //     buyCoefficient = 1f,
            //     sellCoefficient = 0.7f
            // };

            // return _priceCalculator.CalculateSellPrice(input);
    }

        public List<MarketItemData> GetMarketItemsForCurrentSystem()
        {
            List<MarketItemData> result = new List<MarketItemData>();
            IReadOnlyList<ItemConfig> allItems = _configService.GetAllItems();

            Debug.Log("EconomyService | allItems count: " + allItems.Count);

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
        
        private StarSystemConfig GetCurrentSystemConfig()
        {
            if (_gameSessionService == null)
                Debug.Log("EconomyService| _gameSessionService is null");

            if (_gameSessionService.CurrentSave == null)
                Debug.Log("EconomyService| _gameSessionService.CurrentSave is null");

            if (_gameSessionService.CurrentSave.PlayerProfile == null)
                Debug.Log("EconomyService| _gameSessionService.CurrentSave.PlayerProfile is null");

            PlayerProfileData playerProfile = _gameSessionService.CurrentSave.PlayerProfile;
            if (playerProfile == null || string.IsNullOrWhiteSpace(playerProfile.CurrentSystemId))
            {
                return null;
            }

            return _configService.GetStarSystemConfigById(playerProfile.CurrentSystemId);
        }

        // private float GetEconomyModifier(SystemEconomyType economyType, ItemCategory itemCategory)
        // {
        //     switch (economyType)
        //     {
        //         case SystemEconomyType.Mining:
        //             return GetMiningModifier(itemCategory);

        //         case SystemEconomyType.Agricultural:
        //             return GetAgriculturalModifier(itemCategory);

        //         case SystemEconomyType.Industrial:
        //             return GetIndustrialModifier(itemCategory);

        //         case SystemEconomyType.HighTech:
        //             return GetHighTechModifier(itemCategory);

        //         case SystemEconomyType.Frontier:
        //             return GetFrontierModifier(itemCategory);

        //         case SystemEconomyType.Balanced:
        //         default:
        //             return 1.0f;
        //     }
        // }

        // private float GetMiningModifier(ItemCategory category)
        // {
        //     switch (category)
        //     {
        //         case ItemCategory.Minerals: return 0.75f;
        //         case ItemCategory.Industrial: return 0.90f;
        //         case ItemCategory.Luxury: return 1.20f;
        //         case ItemCategory.Technology: return 1.15f;
        //         default: return 1.0f;
        //     }
        // }

        // private float GetAgriculturalModifier(ItemCategory category)
        // {
        //     switch (category)
        //     {
        //         case ItemCategory.Food: return 0.75f;
        //         case ItemCategory.Medicine: return 1.05f;
        //         case ItemCategory.Technology: return 1.15f;
        //         case ItemCategory.Minerals: return 1.05f;
        //         default: return 1.0f;
        //     }
        // }

        // private float GetIndustrialModifier(ItemCategory category)
        // {
        //     switch (category)
        //     {
        //         case ItemCategory.Industrial: return 0.80f;
        //         case ItemCategory.Minerals: return 0.90f;
        //         case ItemCategory.Fuel: return 0.95f;
        //         case ItemCategory.Luxury: return 1.15f;
        //         default: return 1.0f;
        //     }
        // }

        // private float GetHighTechModifier(ItemCategory category)
        // {
        //     switch (category)
        //     {
        //         case ItemCategory.Electronics: return 0.80f;
        //         case ItemCategory.Technology: return 0.75f;
        //         case ItemCategory.Minerals: return 1.20f;
        //         case ItemCategory.Food: return 1.05f;
        //         default: return 1.0f;
        //     }
        // }

        // private float GetFrontierModifier(ItemCategory category)
        // {
        //     switch (category)
        //     {
        //         case ItemCategory.Food: return 1.15f;
        //         case ItemCategory.Medicine: return 1.20f;
        //         case ItemCategory.Fuel: return 1.10f;
        //         case ItemCategory.Luxury: return 1.25f;
        //         default: return 1.05f;
        //     }
        // }

        public int GetCredits()
        {
            return _gameSessionService.CurrentSave.PlayerProfile.Credits;
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

            if (_gameSessionService.CurrentSave.PlayerProfile.Credits < totalPrice)
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

            if (_gameSessionService.CurrentSave.PlayerProfile.Credits < totalPrice)
                return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughCredits, itemId, quantity);

            if (!_inventoryService.CanAddItem(itemId, quantity))
                return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughCargoSpace, itemId, quantity);

            bool addSucceeded = _inventoryService.AddItem(itemId, quantity);
            if (!addSucceeded)
                return EconomyOperationResult.Failed(EconomyErrorCode.NotEnoughCargoSpace, itemId, quantity);

            _gameSessionService.CurrentSave.PlayerProfile.Credits -= totalPrice;
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
            _gameSessionService.CurrentSave.PlayerProfile.Credits += totalPrice;
            PublishCreditsChanged();

            eventBus.Publish(new SaveNeedEvent());
            return EconomyOperationResult.Completed(totalPrice, itemId, quantity);
        }

        public void AddCredits(int amount)
        {
            if (amount <= 0)
                return;

            _gameSessionService.CurrentSave.PlayerProfile.Credits += amount;
            PublishCreditsChanged();
        }

        public bool SpendCredits(int amount)
        {
            if (amount <= 0)
                return false;

            if (_gameSessionService.CurrentSave.PlayerProfile.Credits < amount)
                return false;

            _gameSessionService.CurrentSave.PlayerProfile.Credits -= amount;
            PublishCreditsChanged();
            return true;
        }

        private void PublishCreditsChanged()
        {
            eventBus.Publish(new CreditsChangedEvent(_gameSessionService.CurrentSave.PlayerProfile.Credits));
        }             
    }