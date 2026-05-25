using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketScreenController : CustomMonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject marketScreenRoot;

    [Header("Header")]
    [SerializeField] private TMP_Text cargoText;

    [Header("List")]
    [SerializeField] private Transform itemsContent;
    [SerializeField] private MarketItemRowView marketItemRowPrefab;

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    private SimpleEventBus simpleEventBus;
    private IEconomyService _economyService;
    private IMarketTransactionService _marketTransactionService;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private IInventoryService _inventoryService;

    private ICurrentMarketItemsProvider _marketItemsProvider;
    // private IItemPriceProvider _itemPriceProvider;

    private readonly List<MarketItemRowView> _spawnedRows = new List<MarketItemRowView>();

    public void Initialize()
    {
        simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _economyService = Bootstrapper.Instance.ServiceRegistry.Get<IEconomyService>();
        _marketTransactionService = Bootstrapper.Instance.ServiceRegistry.Get<IMarketTransactionService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _inventoryService = Bootstrapper.Instance.ServiceRegistry.Get<IInventoryService>();

        _marketItemsProvider = new CurrentMarketItemsProvider();

        SubscribeToEvents();

        LogCustom("initialized");
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Subscribe<MarketEnteredEvent>(OnMarketEntered);
        simpleEventBus.Subscribe<CargoChangedEvent>(OnCargoChanged);
    }

    private void UnsubscribeFromEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Unsubscribe<MarketEnteredEvent>(OnMarketEntered);
        simpleEventBus.Unsubscribe<CargoChangedEvent>(OnCargoChanged);
    }  

    private void OnMarketEntered(MarketEnteredEvent evt)
    {
        Refresh();
    }

    private void OnCargoChanged(CargoChangedEvent evt)
    {
        RefreshHeader();
    }    

    // private void Awake()
    // {
    //     Refresh();

    //     if (IsDebug())
    //         Debug.Log("[MarketScreenController] awaked");
    // }

    public void Refresh()
    {
        RefreshHeader();
        RebuildItemList();

        if (IsDebug())
            Debug.Log("[MarketScreenController] refreshed");
    }

    private void RefreshHeader()
    {
        if (_gameSessionService == null)
        {
            if (IsDebug())
                Debug.LogError("MarketScreenController | _gameSessionService is null");
            return;
        }

        if (_gameSessionService.CurrentSave == null)
        {
            if (IsDebug())
                Debug.Log("MarketScreenController | _gameSessionService.CurrentSave is null");
            return;
        }

        if (_gameSessionService.CurrentSave.PlayerProfile == null)
        {
            if (IsDebug())
                Debug.Log("MarketScreenController | _gameSessionService.CurrentSave.PlayerProfile is null");
            return;
        }

        PlayerProfileData playerProfile = _gameSessionService.CurrentSave.PlayerProfile;
        // if (playerProfile == null)
        // {
        //     if (cargoText != null) cargoText.text = 
        //             $"Cargo: {_gameSessionService.CurrentSave.PlayerProfile.CurrentShipRuntimeData.CargoCapacity} / {_gameSessionService.CurrentSave.PlayerProfile.CurrentShipRuntimeData.CargoCapacity}";
        //     return;
        // }

        if (cargoText != null)
        {
            RuntimeCargoInventory cargo = playerProfile.PlayerShipState.GetActiveShip().Cargo;
            int used = cargo != null ? cargo.GetUsedCapacity() : 0;
            int max = playerProfile.PlayerShipState.GetActiveShip().CargoCapacity;

            cargoText.text = $"Cargo: {used} / {max}";
        }        

        if (IsDebug())
            Debug.Log("[MarketScreenController] header refreshed");
    }

    private void RebuildItemList()
    {
        ClearSpawnedRows();

        if (_marketItemsProvider == null)
        {
            if (IsDebug())
                Debug.LogError("[MarketScreenController] MarketItemsProvider is null.");
            return;            
        }
        IReadOnlyList<MarketItemEntry> items = _marketItemsProvider.GetAvailableItems();

        if (IsDebug())
            Debug.Log("[MarketScreenController] Items.Count = " + items.Count);
        foreach (var item in items)
        {
            var row = Instantiate(marketItemRowPrefab, itemsContent);
            _spawnedRows.Add(row);

            string itemId = item.ItemConfig.Id;
            string itemName = item.ItemConfig.DisplayName;

            // _itemPriceProvider.TryGetBuyPrice(itemId, out int buyPrice);
            // _itemPriceProvider.TryGetSellPrice(itemId, out int sellPrice);

            int buyPrice = _economyService.GetBuyPrice(itemId);
            int sellPrice = _economyService.GetSellPrice(itemId);

            int ownedCount = _inventoryService.GetItemCount(itemId);
            bool canBuy = _economyService.CanBuy(itemId, 1);
            bool canSell = _economyService.CanSell(itemId, 1);

            row.Bind(
                item,
                ownedCount,
                buyPrice,
                sellPrice,
                BuyOne,
                SellOne
            );
        }

        if (IsDebug())
            Debug.Log("[MarketScreenController] rows builded");
    }

    private void BuyOne(string itemId)
    {
        if (_economyService == null)
        {
            if (IsDebug())
                Debug.LogError("[MarketScreenController] EconomyService is null.");
            return;
        }

        EconomyOperationResult result = _economyService.BuyItem(itemId, 1);
        if (IsDebug())
            Debug.Log("[MarketScreenController] buy operation result = " + result.Success +
                    " " + result.ErrorCode);

        Refresh();
    }

    private void SellOne(string itemId)
    {
        if (_economyService == null)
        {
            if (IsDebug())
                Debug.LogError("[MarketScreenController] EconomyService is null.");
            return;
        }

        EconomyOperationResult result = _economyService.SellItem(itemId, 1);
        if (IsDebug())
            Debug.Log("[MarketScreenController] buy operation result = " + result.Success +
                    " " + result.ErrorCode);

        Refresh();
    }

    // private void RebuildItemList()
    // {
    //     ClearSpawnedRows();

    //     if (_economyService == null)
    //     {
    //         if (IsDebug())
    //             Debug.Log("[MarketScreenController] economyService is null");
    //         return;
    //     }

    //     if (itemsContent == null)
    //     {
    //         if (IsDebug())
    //             Debug.Log("[MarketScreenController] itemsContent is null");
    //         return;
    //     }

    //     if (marketItemRowPrefab == null)
    //     {
    //         if (IsDebug())
    //             Debug.Log("[MarketScreenController] marketItemRowPrefab is null");
    //         return;
    //     }

    //     List<MarketItemData> marketItems = _economyService.GetMarketItemsForCurrentSystem();
    //     PlayerProfileData playerProfile = _gameSessionService.CurrentSave.PlayerProfile;
    //     CargoInventory cargo = playerProfile != null ? playerProfile.CurrentShipRuntimeData.Cargo : null;

    //     if (IsDebug())
    //         Debug.Log("[MarketScreenController] marketItems.Count = " + marketItems.Count);
    //     for (int i = 0; i < marketItems.Count; i++)
    //     {
    //         MarketItemData itemData = marketItems[i];
    //         if (itemData == null)
    //             continue;

    //         MarketItemRowView row = Instantiate(marketItemRowPrefab, itemsContent);

    //         int ownedQuantity = cargo != null ? cargo.GetQuantity(itemData.itemId) : 0;

    //         row.Bind(
    //             itemData,
    //             ownedQuantity,
    //             OnBuyClicked,
    //             OnSellClicked);

    //         _spawnedRows.Add(row);
    //     }

    //     if (IsDebug())
    //         Debug.Log("[MarketScreenController] rows builded");
    // }

    private void ClearSpawnedRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i].gameObject);

        _spawnedRows.Clear();

        if (IsDebug())
            Debug.Log("[MarketScreenController] rows cleared");
    }

    private void OnBuyClicked(string itemId)
    {
        if (_marketTransactionService == null)
        {
            if (IsDebug())
                Debug.LogError("[MarketScreenController] MarketTransactionService is null.");
            return;
        }

        BuyItemResult result = _marketTransactionService.BuyItem(itemId, 1);
        if (IsDebug())
            Debug.Log($"[MarketScreenController] Buy result: {result.resultType} | item={result.itemId} | total={result.totalPrice}");

        Refresh();
    }

    private void OnSellClicked(string itemId)
    {
        if (_marketTransactionService == null)
        {
            if (IsDebug())
                Debug.LogError("[MarketScreenController] MarketTransactionService is null.");
            return;
        }

        SellItemResult result = _marketTransactionService.SellItem(itemId, 1);
        if (IsDebug())
            Debug.Log($"[MarketScreenController] Sell result: {result.resultType} | item={result.itemId} | total={result.totalPrice}");

        Refresh();
    }
}