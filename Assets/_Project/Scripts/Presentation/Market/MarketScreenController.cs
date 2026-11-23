using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketScreenController : CustomMonoBehaviour
{
    // Existing scene bindings remain valid; the legacy list is retired by the shared view.
    [SerializeField] private GameObject marketScreenRoot;
    [SerializeField] private TMP_Text cargoText;
    [SerializeField] private Transform itemsContent;
    [SerializeField] private MarketItemRowView marketItemRowPrefab;
    [SerializeField] private Button backButton;
    [SerializeField] private MarketVisualCatalog2A marketVisuals;
    private IPlanetMarketService _market;
    private int _purchaseQuantity = 1, _saleQuantity = 1, _purchaseMaximum, _saleMaximum;
    private IGameSessionService _session;
    private IConfigService _config;
    private SimpleEventBus _events;
    private MarketPanelView2A _view;
    private bool _initialized, _subscribed, _busy, _dirty, _warehouse;
    private MarketKindFilter2A _kind = MarketKindFilter2A.All;
    private string _category = "all", _leftKey, _rightKey, _location;
    private GameRuntimeState _boundState;
    private MarketRuntimeData _boundMarket;
    private long _revision;
    private readonly List<MarketCardData2A> _offers = new List<MarketCardData2A>(), _owned = new List<MarketCardData2A>();
    private readonly List<MarketCardData2A> _left = new List<MarketCardData2A>(), _right = new List<MarketCardData2A>();
    private readonly List<KeyValuePair<string, string>> _categories = new List<KeyValuePair<string, string>>();
    private readonly List<WeaponType> _alliedWeaponTypes = new List<WeaponType>();
    private readonly HashSet<string> _seen = new HashSet<string>();
    private bool Visible => _initialized && isActiveAndEnabled && _view != null && _view.IsReady;
    public void Initialize()
    {
        if (_initialized) return;
        var registry = Bootstrapper.Instance.ServiceRegistry;
        _market = registry.Get<IPlanetMarketService>(); _session = registry.Get<IGameSessionService>();
        _config = registry.Get<IConfigService>(); _events = registry.Get<SimpleEventBus>();
        MarketScreenRules2A.CollectAlliedWeaponTypes(_config.EquipmentCatalog, _alliedWeaponTypes);
        TMP_Text template = cargoText != null ? cargoText : GetComponentInChildren<TMP_Text>(true);
        if (template == null && marketItemRowPrefab != null) template = marketItemRowPrefab.GetComponentInChildren<TMP_Text>(true);
        if (template == null && transform.parent != null) template = transform.parent.GetComponentInChildren<TMP_Text>(true);
        _view = GetComponent<MarketPanelView2A>() ?? gameObject.AddComponent<MarketPanelView2A>();
        _view.Build(template, kind => { _kind = kind; _category = "all"; Refresh(); _view.ResetCategoryScroll(); _view.ResetScrolls(); },
            category => { _category = category; Refresh(); _view.ResetScrolls(); }, warehouse => { _warehouse = warehouse; _saleQuantity = 1; _rightKey = null; Refresh(); _view.ResetScrolls(); },
            key => { if (_leftKey != key) _purchaseQuantity = 1; _leftKey = key; Refresh(); }, key => { if (_rightKey != key) _saleQuantity = 1; _rightKey = key; Refresh(); },
            () => Execute(true), () => Execute(false), Transfer, ChangePurchaseQuantity, MaxPurchaseQuantity, ChangeSaleQuantity, MaxSaleQuantity, MinPurchaseQuantity, MinSaleQuantity);
        _initialized = true; Subscribe(); RequestRefresh();
    }
    private void OnEnable() { if (_initialized) { Subscribe(); RequestRefresh(); } }
    private void OnDisable() { Unsubscribe(); _boundState = null; _boundMarket = null; }
    private void OnDestroy() { _initialized = false; Unsubscribe(); }
    private void Subscribe()
    {
        if (_subscribed || !Visible || _events == null) return;
        _events.Subscribe<MarketEnteredEvent>(Entered); _events.Subscribe<MarketStateChangedEvent>(Changed);
        _events.Subscribe<CargoChangedEvent>(CargoChanged); _events.Subscribe<CreditsChangedEvent>(CreditsChanged);
        _events.Subscribe<ActiveShipChangedEvent>(ShipChanged); _subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!_subscribed || _events == null) return;
        _events.Unsubscribe<MarketEnteredEvent>(Entered); _events.Unsubscribe<MarketStateChangedEvent>(Changed);
        _events.Unsubscribe<CargoChangedEvent>(CargoChanged); _events.Unsubscribe<CreditsChangedEvent>(CreditsChanged);
        _events.Unsubscribe<ActiveShipChangedEvent>(ShipChanged); _subscribed = false;
    }
    private void Entered(MarketEnteredEvent evt) => RequestRefresh();
    private void CargoChanged(CargoChangedEvent evt) => RequestRefresh();
    private void CreditsChanged(CreditsChangedEvent evt) => RequestRefresh();
    private void ShipChanged(ActiveShipChangedEvent evt) => RequestRefresh();
    private void Changed(MarketStateChangedEvent evt)
    {
        if (_boundMarket != null && evt.SystemId == _boundMarket.SystemId && evt.PlanetId == _boundMarket.PlanetId) RequestRefresh();
    }
    private void RequestRefresh() { if (!_busy && Visible) _dirty = true; }
    private void LateUpdate() { if (_dirty && Visible && !_busy) Refresh(); }
    public void Refresh()
    {
        if (!Visible || _busy) return;
        _dirty = false;
        var state = _session.State; var player = state?.Player;
        string location = player == null ? "" : player.CurrentSystemId + "|" + player.CurrentPlanetId + "|" + player.CurrentStationId;
        if (_location != location || !ReferenceEquals(_boundState, state))
        { _purchaseQuantity = _saleQuantity = 1; _leftKey = _rightKey = null; _warehouse = false; _location = location; }
        _boundState = state; _boundMarket = _market.CurrentMarket;
        bool station = !string.IsNullOrEmpty(player?.CurrentStationId);
        var stationConfig = StationLocationRules.GetDockedStation(
            player, _config.GetStarSystemConfigById(player?.CurrentSystemId));
        var planet = !station ? _config.GetPlanetConfigById(player?.CurrentPlanetId) : null;
        var illustration = stationConfig?.MarketIllustration ?? planet?.PlanetTypeConfig?.MarketIllustration ??
            marketVisuals?.Get(planet, stationConfig);
        _view.SetHeader(illustration);
        BuildOffers(); BuildOwned(); BuildCategories();
        _left.Clear(); _right.Clear();
        foreach (var item in _offers) if (item.Matches(_kind, _category)) _left.Add(item);
        foreach (var item in _owned) if (item.Matches(_kind, _category)) _right.Add(item);
        if (Find(_left, _leftKey) == null) _leftKey = null;
        if (Find(_right, _rightKey) == null) _rightKey = null;
        _view.SetFilters(_kind, _categories, _category, _warehouse, station);
        _view.SetCards(_left, _right, _leftKey, _rightKey);
        var buy = Find(_left, _leftKey); var sell = Find(_right, _rightKey);
        _purchaseMaximum = buy == null ? 0 : buy.Equipment ? 1 : _market.GetMaximumTradeQuantity(buy.Id, true, _warehouse);
        // The right selector serves both sale and storage transfer; it must not depend on market demand or wallet headroom.
        _saleMaximum = sell == null ? 0 : sell.Equipment ? 1 : Math.Max(0, sell.TradeableQuantity >= 0 ? sell.TradeableQuantity : sell.Quantity);
        _purchaseQuantity = buy != null && (buy.Equipment || buy.Component) ? 1 : ClampQuantity(_purchaseQuantity, _purchaseMaximum);
        _saleQuantity = sell != null && sell.Equipment ? 1 : ClampQuantity(_saleQuantity, _saleMaximum);
        long totalPrice = buy == null ? 0 : buy.Equipment ? buy.Price : _market.GetTotalPrice(buy.Id, _purchaseQuantity, true);
        long salePrice = sell == null ? 0 : sell.Equipment ? sell.Price : _market.GetTotalPrice(sell.Id, _saleQuantity, false);
        bool canBuy = buy != null && buy.Quantity > 0 && (buy.Hull ? _market.CanTradeHull(buy.Id, true, _warehouse) : buy.Equipment ? _market.CanTradeEquipment(buy.Id, true, _warehouse) : _market.CanTrade(buy.Id, _purchaseQuantity, true, _warehouse));
        bool canSell = sell != null && (sell.Hull ? _market.CanTradeHull(sell.Id, false, _warehouse) : sell.Equipment ? _market.CanTradeEquipment(sell.Id, false, _warehouse) : _market.CanTrade(sell.Id, _saleQuantity, false, _warehouse));
        bool canTransfer = sell != null && !sell.Hull && !sell.Locked &&
            _market.CanTransfer(sell.Id, sell.Equipment, !_warehouse, sell.Equipment ? 1 : _saleQuantity);
        string buyInfo = buy == null ? "Выбери товар на рынке" : "";
        string sellInfo = sell == null ? "Выбери предмет" : sell.Locked ? "Миссионный груз" : "";
        _view.SetPurchase(buy != null && !buy.Equipment && !buy.Component, _purchaseQuantity, _purchaseMaximum, totalPrice);
        _view.SetSale(sell != null && !sell.Equipment, _saleQuantity, _saleMaximum, salePrice);
        _view.SetActions(buyInfo, sellInfo, canBuy, canSell, canTransfer);
        _revision = _boundMarket?.Revision ?? -1;
    }
    private static bool IsVisibleMarketGoods(ItemConfig item) =>
        item != null && item.ItemCategory != ItemCategory.Fuel &&
        !LocationServicePrices.IsServiceItem(item.Id);

    private void BuildOffers()
    {
        _offers.Clear(); if (_boundMarket == null) return;
        foreach (var entry in _market.GetItems())
            if (IsVisibleMarketGoods(entry?.ItemConfig)) _offers.Add(Goods(entry.ItemConfig, entry.Quantity, true));
        foreach (var listing in _boundMarket.Equipment)
            if (listing?.Equipment != null && listing.Allied && listing.ExpiresAtSeconds > _boundMarket.ElapsedSeconds)
                _offers.Add(Equipment(listing.Equipment, true));
        foreach (var listing in _boundMarket.Hulls)
            if (listing?.Hull != null && listing.ExpiresAtSeconds > _boundMarket.ElapsedSeconds) _offers.Add(Hull(listing.Hull, true));
    }
    private void BuildOwned()
    {
        _owned.Clear(); if (_boundMarket == null) return;
        var ship = _session.State?.Player?.GetActiveShip();
        var cargo = _warehouse ? _boundMarket.PersonalStorage : ship?.Cargo;
        var equipment = _warehouse ? _boundMarket.PersonalEquipment : ship?.CargoEquipment;
        _seen.Clear();
        if (cargo?.entries != null)
            foreach (var entry in cargo.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || !_seen.Add(entry.ItemId)) continue;
                int quantity = cargo.GetQuantity(entry.ItemId); var config = _config.GetItemConfigById(entry.ItemId);
                if (quantity > 0 && IsVisibleMarketGoods(config) && config is not ShipEquipmentConfig)
                {
                    var card = Goods(config, quantity, false);
                    card.TradeableQuantity = cargo.GetTradeableQuantity(entry.ItemId);
                    _owned.Add(card);
                }
            }
        var hulls = _warehouse ? _boundMarket.PersonalHulls : ship?.CargoHulls;
        if (hulls != null)
            foreach (var hull in hulls) if (hull != null) _owned.Add(Hull(hull, false));
        if (equipment != null) foreach (var item in equipment) if (item != null) _owned.Add(Equipment(item, false));
    }
    private MarketCardData2A Goods(ItemConfig config, int quantity, bool buy)
    {
        var component = config as ShipEquipmentConfig;
        return new MarketCardData2A {
            Key = "goods:" + config.Id, Id = config.Id, Title = config.DisplayName, Icon = config.Icon, Quantity = quantity,
            Component = component != null, Price = _market.GetPrice(config.Id, buy), CargoSize = config.CargoSize,
            CategoryKey = component != null ? MarketScreenRules2A.ComponentCategory(component.Slot) : "goods:" + config.ItemCategory,
            CategoryTitle = MarketScreenRules2A.GoodsName(config.ItemCategory),
            Info = HangarScreenController.WithDescription(config.Description, HangarScreenController.CargoSpaceLine(config.CargoSize)),
            CurrentDurability = component?.MaximumDurability ?? 0,
            MaximumDurability = component?.MaximumDurability ?? 0 };
    }
    private MarketCardData2A Equipment(SystemNpcWeaponRuntimeState item, bool buy)
    {
        if (item.IsComponent)
        {
            var component = _config.GetItemConfigById(item.ComponentConfigId) as ShipEquipmentConfig;
            return new MarketCardData2A { Key = "module:" + item.InstanceId, Id = item.InstanceId,
                Title = component != null ? component.DisplayName : item.ComponentConfigId, Icon = component?.Icon,
                Info = HangarScreenController.ComponentInfo(item, component),
                Level = component?.Level ?? 0, LevelColor = LevelColor(component?.Level ?? 1),
                Component = true, Equipment = true, Quantity = 1, Price = _market.EquipmentPrice(item, buy),
                CargoSize = item.RolledStats.CargoSize, CurrentDurability = item.CurrentDurability,
                MaximumDurability = item.MaximumDurability,
                CategoryKey = MarketScreenRules2A.ComponentCategory(component?.Slot), CategoryTitle = "Модули" };
        }
        var config = _config.GetWeaponConfigById(item.WeaponConfigId);
        WeaponType type = config != null ? config.WeaponType : item.RolledStats.WeaponType;
        Sprite icon = config != null ? config.Icon : null;
        if (icon == null && marketVisuals != null) icon = type == WeaponType.Missile ? marketVisuals.MissileIcon : marketVisuals.EquipmentIcon;
        return new MarketCardData2A { Key = "equipment:" + item.InstanceId, Id = item.InstanceId,
            Title = config != null ? config.DisplayName : item.WeaponConfigId, Equipment = true, Icon = icon, Quantity = 1,
            Level = config?.Level ?? item.RolledStats.Level, LevelColor = LevelColor(config?.Level ?? item.RolledStats.Level),
            Info = HangarScreenController.WeaponInfo(item, config),
            Price = _market.EquipmentPrice(item, buy), CargoSize = item.RolledStats.CargoSize > 0 ? item.RolledStats.CargoSize : config?.CargoSize ?? 1,
            CurrentDurability = item.CurrentDurability, MaximumDurability = item.MaximumDurability, CategoryKey = "equipment:" + type, CategoryTitle = MarketScreenRules2A.EquipmentName(type) };
    }
    private MarketCardData2A Hull(HullEquipmentRuntimeState item, bool buy)
    {
        var config = _market.GetHullConfig(item.ConfigId);
        return new MarketCardData2A { Key = "hull:" + item.InstanceId, Id = item.InstanceId, Equipment = true, Hull = true,
            Title = config != null ? config.DisplayName : item.ConfigId,
            Icon = config?.Icon, Quantity = 1,
            Level = config?.Level ?? 0, LevelColor = LevelColor(config?.Level ?? 1),
            Info = HangarScreenController.WithDescription(config?.Description, $"Вместимость трюма: {item.CargoCapacity}"),
            Price = _market.HullPrice(item, buy),
            CurrentDurability = item.CurrentDurability, MaximumDurability = item.MaximumDurability,
            CategoryKey = "equipment:hull", CategoryTitle = "Корпус" };
    }
    private Color LevelColor(int level) => _config.GameVisualConfig?.GetEquipmentLevelColor(level) ?? Color.white;

    private void BuildCategories()
    {
        MarketScreenRules2A.BuildCategories(_kind, _offers, _owned, _categories, _seen, _alliedWeaponTypes);
        bool found = false;
        foreach (var pair in _categories) if (pair.Key == _category) { found = true; break; }
        if (!found) _category = "all";
    }

    private static MarketCardData2A Find(List<MarketCardData2A> items, string key) => key == null ? null : items.Find(x => x.Key == key);
    private bool HasCurrentContext() => ReferenceEquals(_boundState, _session.State) && ReferenceEquals(_boundMarket, _market.CurrentMarket);
    private static int ClampQuantity(long quantity, int maximum) => maximum <= 0 ? 0 : (int)Math.Max(1, Math.Min(maximum, quantity));
    private void ChangePurchaseQuantity(int delta)
    {
        if (_busy || Find(_left, _leftKey)?.Equipment != false) return;
        _purchaseQuantity = ClampQuantity((long)_purchaseQuantity + delta, _purchaseMaximum);
        Refresh();
    }
    private void MaxPurchaseQuantity()
    {
        if (_busy || Find(_left, _leftKey)?.Equipment != false) return;
        _purchaseQuantity = _purchaseMaximum; Refresh();
    }
    private void ChangeSaleQuantity(int delta)
    {
        if (_busy || Find(_right, _rightKey)?.Equipment != false) return;
        _saleQuantity = ClampQuantity((long)_saleQuantity + delta, _saleMaximum);
        Refresh();
    }
    private void MaxSaleQuantity()
    {
        if (_busy || Find(_right, _rightKey)?.Equipment != false) return;
        _saleQuantity = _saleMaximum; Refresh();
    }
    private void MinPurchaseQuantity()
    {
        if (_busy || Find(_left, _leftKey)?.Equipment != false) return;
        _purchaseQuantity = ClampQuantity(1, _purchaseMaximum); Refresh();
    }
    private void MinSaleQuantity()
    {
        if (_busy || Find(_right, _rightKey)?.Equipment != false) return;
        _saleQuantity = ClampQuantity(1, _saleMaximum); Refresh();
    }
    private void Execute(bool buy)
    {
        if (_busy || !Visible) return;
        var selected = Find(buy ? _left : _right, buy ? _leftKey : _rightKey); if (selected == null) return;
        if (!HasCurrentContext()) { Refresh(); return; }
        _busy = true;
        try
        {
            string operation = Guid.NewGuid().ToString("N"); string reason;
            bool success;
            int quantity = selected.Equipment || selected.Component ? 1 : buy ? _purchaseQuantity : _saleQuantity;
            long total = selected.Equipment ? selected.Price : _market.GetTotalPrice(selected.Id, quantity, buy);
            using (var observation = PlayerAnalyticsSafe.Observe(buy ? PlayerAnalyticsAction.BuyItem : PlayerAnalyticsAction.SellItem, selected.Id, quantity))
            {
                if (selected.Hull) success = _market.TradeHull(selected.Id, buy, operation, _revision, out reason, _warehouse);
                else if (selected.Equipment) success = _market.TradeEquipment(selected.Id, buy, operation, _revision, out reason, _warehouse);
                else { var result = _market.Trade(selected.Id, quantity, buy, operation, _revision, _warehouse); success = result.Success; reason = result.ErrorCode.ToString(); }
                observation.Complete(success, reason, success ? (int)Math.Min(int.MaxValue, total) : 0);
            }
            AppLog.Info(DebugLogChannel.Market, "[MARKET_UI_TRADE] Buy=" + buy + " | Quantity=" + quantity + " | Success=" + success + " | Reason=" + reason);
        }
        finally { _busy = false; Refresh(); }
    }
    private void Transfer()
    {
        if (_busy || !Visible) return;
        var selected = Find(_right, _rightKey);
        if (selected == null || selected.Hull) return;
        if (!HasCurrentContext()) { Refresh(); return; }
        _busy = true;
        try
        {
            int quantity = selected.Equipment ? 1 : _saleQuantity;
            string operation = Guid.NewGuid().ToString("N");
            bool success = _market.Transfer(selected.Id, selected.Equipment, !_warehouse, quantity, operation, _revision, out string reason);
            AppLog.Info(DebugLogChannel.Market, "[MARKET_UI_TRANSFER] Quantity=" + quantity + " | Success=" + success + " | Reason=" + reason);
        }
        finally { _busy = false; Refresh(); }
    }
}
