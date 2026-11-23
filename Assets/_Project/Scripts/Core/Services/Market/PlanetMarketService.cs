using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Only transaction authority for market goods and individual cargo equipment.</summary>
public sealed partial class PlanetMarketService : IPlanetMarketService
{
    private readonly IGameSessionService _session;
    private readonly IConfigService _config;
    private readonly IInventoryService _inventory;
    private readonly SimpleEventBus _events;
    private bool _committing;
    private bool _advancing;
    private GameRuntimeState _boundState;
    private List<MarketRuntimeData> _boundMarkets;
    private IReadOnlyList<StarSystemConfig> _systems = Array.Empty<StarSystemConfig>();
    private readonly Dictionary<(string System, string Location), MarketRuntimeData> _marketIndex = new Dictionary<(string, string), MarketRuntimeData>();
    private readonly Dictionary<MarketRuntimeData, double> _equipmentDeadlines = new Dictionary<MarketRuntimeData, double>();
    public int LastScheduledSystemCount { get; private set; }
    public int LastProcessedMarketCount { get; private set; }
    public int LastEquipmentUpdateCount { get; private set; }
    private int _lastScheduleLogFrame = int.MinValue;
    private readonly List<MarketStateChangedEvent> _changedMarkets = new List<MarketStateChangedEvent>();
    public PlanetMarketService(IGameSessionService session, IConfigService config, IInventoryService inventory, SimpleEventBus events)
    {
        _session = session; _config = config; _inventory = inventory; _events = events;
        _events?.Subscribe<ResearchCompletedEvent>(OnResearchCompleted);
    }
    private GameRuntimeState State => _session.State;
    private int? _marketLevelOverride;
    private int MarketLevel => _marketLevelOverride ?? Math.Max(1, Math.Min(10, ResearchLevel(ResearchUpgrade.TradeNewGoods)));
    private int ResearchLevel(ResearchUpgrade upgrade) => upgrade == ResearchUpgrade.None ? 0 : State?.Galaxy?.Research?.GetLevel(upgrade) ?? 0;
    private ShipRuntimeData Ship => State?.Player?.PlayerShipState?.GetActiveShip();

    public void InitializeSession()
    {
        var state = State;
        if (state == null) return;
        state.Meta ??= new GameRuntimeMetaState();
        state.Markets ??= new List<MarketRuntimeData>();
        if (ReferenceEquals(_boundState, state) && ReferenceEquals(_boundMarkets, state.Markets)) return;
        _boundState = state; _boundMarkets = state.Markets;
        _marketIndex.Clear(); _equipmentDeadlines.Clear();
        _systems = _config.GetAllStarSystems() ?? Array.Empty<StarSystemConfig>();
        for (int i = 0; i < state.Markets.Count; i++)
        {
            var market = state.Markets[i];
            if (market != null) _marketIndex[(market.SystemId ?? "", market.PlanetId ?? "")] = market;
        }
        // Seed markets once, before their first deferred update. No world scan in the steady-state frame path.
        for (int i = 0; i < _systems.Count; i++)
        {
            var system = _systems[i];
            if (system == null) continue;
            var planets = system.PlanetRefs;
            if (planets != null)
                for (int j = 0; j < planets.Length; j++)
                    if (planets[j]?.MarketProfile != null) Bind(system.Id, planets[j].Id, planets[j].MarketProfile, true);
            var stationProfile = StationProfile(system);
            if (stationProfile != null && system.Station != null) Bind(system.Id, "station:" + system.Station.Id, stationProfile, true);
        }
    }
    private static MarketProfileConfig StationProfile(StarSystemConfig system)
    {
        if (system.Station == null) return null;
        if (system.Station.MarketProfile != null) return system.Station.MarketProfile;
        var planets = system.PlanetRefs;
        if (planets != null)
            for (int i = 0; i < planets.Length; i++)
                if (planets[i] != null && planets[i].IsInhabited && planets[i].MarketProfile != null) return planets[i].MarketProfile;
        return null;
    }
    private MarketRuntimeData Bind(string systemId, string locationId, MarketProfileConfig profile, bool initialize = false)
    {
        if (State == null || profile == null) return null;
        if (!ReferenceEquals(_boundState, State) || !ReferenceEquals(_boundMarkets, State.Markets)) InitializeSession();
        var key = (systemId, locationId);
        bool created = !_marketIndex.TryGetValue(key, out var market);
        if (created)
        {
            market = new MarketRuntimeData { SystemId = systemId, PlanetId = locationId,
                LastProcessedSimulationSeconds = State.Meta.MarketSimulationSeconds };
            State.Markets.Add(market); _marketIndex.Add(key, market);
        }
        market.Items ??= new List<MarketRuntimeItemState>();
        market.Equipment ??= new List<MarketEquipmentListing>();
        market.Receipts ??= new List<MarketTransactionReceipt>();
        market.PersonalStorage ??= new RuntimeCargoInventory();
        market.PersonalStorage.entries ??= new List<RuntimeCargoEntryData>();
        market.PersonalEquipment ??= new List<SystemNpcWeaponRuntimeState>();
        market.PersonalHulls ??= new List<HullEquipmentRuntimeState>();
        market.Hulls ??= new List<MarketHullListing>();
        int removedEnemyOffers = market.Equipment.RemoveAll(x => x?.Equipment != null && IsEnemyEquipment(x.Equipment));
        removedEnemyOffers += market.Hulls.RemoveAll(x => x?.Hull != null &&
            FindHullConfig(profile, x.Hull.ConfigId)?.MarketAvailable != true);
        if (removedEnemyOffers > 0) market.Revision++;
        var level = profile.GetLevel(MarketLevel);
        int applied = level?.GalaxyLevel ?? 0;
        bool changed = created || market.AppliedGalaxyLevel != applied || market.AppliedProfileId != profile.Id;
        if (changed && market.EquipmentPendingSeconds > 0)
        {
            var oldSettings = profile.GetLevel(market.AppliedGalaxyLevel);
            if (oldSettings != null) FlushEquipment(market, oldSettings, profile.OfferRefreshIntervalSeconds);
        }
        if (initialize || changed || !market.OffersInitialized)
        {
            if (changed || !market.OffersInitialized)
            {
                market.EquipmentPendingSeconds = 0;
                market.OfferRefreshRemainder = 0;
                market.OfferRefreshSequence++;
                RefreshOffers(market, profile, level, market.ElapsedSeconds);
                _equipmentDeadlines.Remove(market);
            }
            var entries = level?.Items;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry?.ItemConfig != null && Stock(market, entry.ItemConfig.Id) == null)
                        market.Items.Add(new MarketRuntimeItemState { ItemId = entry.ItemConfig.Id, Quantity = Math.Max(0, entry.Quantity) });
                }
        }
        if (changed)
        {
            market.AppliedGalaxyLevel = applied; market.AppliedProfileId = profile.Id; market.Revision++;
            _equipmentDeadlines.Remove(market);
            if (_advancing) _changedMarkets.Add(new MarketStateChangedEvent(market.SystemId, market.PlanetId));
            Log("PROFILE", market, "MarketLevel=" + MarketLevel + " | SettingsLevel=" + applied);
        }
        return market;
    }
    private bool Current(out MarketRuntimeData market, out MarketProfileConfig profile)
    {
        market = null; profile = null;
        var player = State?.Player;
        if (player == null || string.IsNullOrEmpty(player.CurrentSystemId)) return false;
        var system = _config.GetStarSystemConfigById(player.CurrentSystemId);
        if (system == null) return false;
        string location = player.CurrentPlanetId;
        if (!string.IsNullOrEmpty(player.CurrentStationId))
        {
            profile = StationLocationRules.Market(State, system);
            location = "station:" + player.CurrentStationId;
        }
        else profile = (system.PlanetRefs ?? Array.Empty<PlanetConfig>()).FirstOrDefault(p => p != null && p.Id == location)?.MarketProfile;
        if (profile == null || string.IsNullOrEmpty(location)) return false;
        market = Bind(system.Id, location, profile);
        // A newly entered or previously deferred system must be current before quoting or trading.
        if (market != null && !_advancing && !_committing)
        {
            _advancing = true;
            try { CatchUp(market, profile); }
            finally { _advancing = false; }
        }
        return market != null;
    }
    public MarketRuntimeData CurrentMarket => Current(out var market, out _) ? market : null;
    private bool IsItemUnlocked(ItemConfig item) =>
        (!(item is ShipEquipmentConfig equipment) || !equipment.EnemyOnly) &&
        item.MarketDevelopmentLevel <= MarketLevel;

    private MarketItemEntry Entry(MarketProfileConfig profile, string id) =>
        profile.GetLevel(MarketLevel)?.Items?.Find(x => x?.ItemConfig != null && x.ItemConfig.Id == id && IsItemUnlocked(x.ItemConfig));
    private static MarketRuntimeItemState Stock(MarketRuntimeData market, string id) => market.Items.Find(x => x != null && x.ItemId == id);

    public IReadOnlyList<MarketItemEntry> GetItems()
    {
        if (!Current(out var market, out var profile)) return Array.Empty<MarketItemEntry>();
        return (profile.GetLevel(MarketLevel)?.Items ?? new List<MarketItemEntry>())
            .Where(x => x?.ItemConfig != null && IsItemUnlocked(x.ItemConfig)).Select(x => new MarketItemEntry {
                ItemConfig = x.ItemConfig, Quantity = Stock(market, x.ItemConfig.Id)?.Quantity ?? 0,
                StockChange = x.StockChange, MaximumStock = x.MaximumStock, BasePrice = x.BasePrice,
                MaximumPrice = x.MaximumPrice }).ToList();
    }
    public int GetPrice(string itemId, bool buy)
    {
        if (!Current(out var market, out var profile)) return 0;
        var entry = Entry(profile, itemId);
        if (entry == null) return 0;
        int stock = Stock(market, itemId)?.Quantity ?? 0;
        // Sale quote includes the incoming unit. The reverse trade uses the same marginal price.
        return MarketEconomyRules.Price(entry, buy ? stock : (int)Math.Min(int.MaxValue, (long)stock + 1));
    }
    public long GetTotalPrice(string itemId, int quantity, bool buy)
    {
        return quantity > 0 && Current(out var market, out var profile)
            ? Quote(market, profile, itemId, quantity, buy) : 0;
    }
    private long Quote(MarketRuntimeData market, MarketProfileConfig profile, string id, int quantity, bool buy)
    {
        var entry = Entry(profile, id);
        var stock = Stock(market, id);
        if (entry == null || stock == null || quantity <= 0) return 0;
        return MarketEconomyRules.TotalPrice(entry, stock.Quantity, quantity, buy);
    }
    public int GetMaximumTradeQuantity(string itemId, bool buy, bool warehouse = false)
    {
        if (_committing || Ship?.Cargo == null || !Current(out var market, out _)) return 0;
        int high = buy ? Stock(market, itemId)?.Quantity ?? 0
            : (warehouse ? market.PersonalStorage : Ship.Cargo).GetTradeableQuantity(itemId);
        int low = 0;
        // Monotonic checks include stock, credits, physical cargo size and integer limits.
        // A binary search avoids a per-unit scan even for large stocks.
        while (low < high)
        {
            int middle = low + (int)(((long)high - low + 1) / 2);
            if (CanTrade(itemId, middle, buy, warehouse)) low = middle;
            else high = middle - 1;
        }
        return low;
    }
    public bool CanTrade(string itemId, int quantity, bool buy, bool warehouse = false)
    {
        if (_committing || Ship?.Cargo == null || quantity <= 0 || !Current(out var market, out var profile)) return false;
        if (Entry(profile, itemId)?.ItemConfig is ShipEquipmentConfig && (!buy || quantity != 1)) return false;
        int price = GetPrice(itemId, buy);
        var stock = Stock(market, itemId);
        long total = Quote(market, profile, itemId, quantity, buy);
        if (price <= 0 || total > int.MaxValue || stock == null || Entry(profile, itemId) == null) return false;
        return buy ? stock.Quantity >= quantity && State.Player.Credits >= total && (warehouse ? CanAddStorage(market, itemId, quantity) : _inventory.CanAddItem(itemId, quantity))
            : (warehouse ? market.PersonalStorage : Ship.Cargo).GetTradeableQuantity(itemId) >= quantity && (long)State.Player.Credits + total <= int.MaxValue && (long)stock.Quantity + quantity <= int.MaxValue;
    }
    public EconomyOperationResult Trade(string itemId, int quantity, bool buy, string operationId = null, long expectedRevision = -1, bool warehouse = false)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return Fail(EconomyErrorCode.InvalidItemId);
        if (quantity <= 0) return Fail(EconomyErrorCode.InvalidQuantity);
        if (_committing) return Fail(EconomyErrorCode.TransactionBusy);
        if (!Current(out var market, out var profile)) return Fail(EconomyErrorCode.PriceNotFound);
        operationId ??= Guid.NewGuid().ToString("N");
        string intent = (buy ? "buy:" : "sell:") + itemId + ":" + quantity + (warehouse ? ":storage" : "");
        var receiptMarket = State.Markets.Find(m => m?.Receipts?.Any(x => x != null && x.OperationId == operationId) == true);
        if (receiptMarket != null && !ReferenceEquals(receiptMarket, market)) return Fail(EconomyErrorCode.OperationConflict);
        var receipt = market.Receipts.Find(x => x != null && x.OperationId == operationId);
        if (receipt != null)
            return receipt.Intent == intent ? EconomyOperationResult.Completed(buy ? -receipt.TotalPrice : receipt.TotalPrice, itemId, quantity) : Fail(EconomyErrorCode.OperationConflict);
        if (expectedRevision >= 0 && market.Revision != expectedRevision) return Fail(EconomyErrorCode.MarketChanged);
        if (Ship?.Cargo == null) return Fail(EconomyErrorCode.ItemNotOwned);
        var component = Entry(profile, itemId)?.ItemConfig as ShipEquipmentConfig;
        if (component != null && quantity != 1) return Fail(EconomyErrorCode.InvalidQuantity);
        if (!buy && component != null) return Fail(EconomyErrorCode.ItemNotOwned);
        int price = GetPrice(itemId, buy);
        var stock = Stock(market, itemId);
        long total = Quote(market, profile, itemId, quantity, buy);
        if (price <= 0 || stock == null || Entry(profile, itemId) == null) return Fail(EconomyErrorCode.PriceNotFound);
        if (total > int.MaxValue || (!buy && ((long)State.Player.Credits + total > int.MaxValue || (long)stock.Quantity + quantity > int.MaxValue))) return Fail(EconomyErrorCode.ValueOverflow);
        if (buy && stock.Quantity < quantity) return Fail(EconomyErrorCode.NotEnoughMarketStock);
        if (buy && State.Player.Credits < total) return Fail(EconomyErrorCode.NotEnoughCredits);
        if (buy && !(warehouse ? CanAddStorage(market, itemId, quantity) : _inventory.CanAddItem(itemId, quantity))) return Fail(EconomyErrorCode.NotEnoughCargoSpace);
        if (!buy && (warehouse ? market.PersonalStorage : Ship.Cargo).GetTradeableQuantity(itemId) < quantity) return Fail(EconomyErrorCode.NotEnoughItemsToSell);
        _committing = true;
        try
        {
            var cargo = warehouse ? market.PersonalStorage : Ship.Cargo;
            bool changed;
            if (component != null)
            {
                Ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
                var equipment = warehouse ? market.PersonalEquipment : Ship.CargoEquipment;
                equipment.Add(SystemNpcWeaponRuntimeState.NewComponent(component, Guid.NewGuid().ToString("N")));
                changed = true;
            }
            else changed = buy ? (warehouse ? cargo.AddLootItem(itemId, quantity) : cargo.AddItem(itemId, quantity, Ship.CargoCapacity)) : cargo.RemoveItem(itemId, quantity);
            if (!changed) return Fail(buy ? EconomyErrorCode.NotEnoughCargoSpace : EconomyErrorCode.NotEnoughItemsToSell);
            State.Player.Credits += buy ? -(int)total : (int)total;
            stock.Quantity += buy ? -quantity : quantity;
            market.Revision++;
            market.Receipts.Add(new MarketTransactionReceipt { OperationId = operationId, Intent = intent, TotalPrice = (int)total, Quantity = quantity });
            Log("TRADE", market, intent + " | Total=" + total + " | Operation=" + operationId);
            Notify("market_trade");
            return EconomyOperationResult.Completed(buy ? -(int)total : (int)total, itemId, quantity);
        }
        finally { _committing = false; }
        EconomyOperationResult Fail(EconomyErrorCode error)
        {
            Log("REJECTED", null, "Item=" + itemId + " | Quantity=" + quantity + " | Reason=" + error);
            return EconomyOperationResult.Failed(error, itemId, quantity);
        }
    }

    // Exact full-world catch-up for offline progress and deterministic integration checks.
    public void AdvanceTime(double seconds)
    {
        if (!BeginAdvance(seconds)) return;
        try
        {
            for (int i = 0; i < _systems.Count; i++) AdvanceSystem(_systems[i]);
        }
        finally { _advancing = false; }
        PublishChanges();
    }
    // Online path shares the movement/combat/behavior selection; it never advances their cursor independently.
    public void AdvanceScheduledTime(double seconds, int quantTick, IGalaxyNpcSimulationScheduleService schedule)
    {
        if (!BeginAdvance(seconds)) return;
        try
        {
            var current = schedule != null ? schedule.GetCurrentSystem() : _config.GetStarSystemConfigById(State.Player?.CurrentSystemId);
            AdvanceSystem(current);
            var selected = schedule?.GetScheduledOffscreenSystems(quantTick);
            if (selected != null)
                for (int i = 0; i < selected.Count; i++)
                {
                    var system = selected[i];
                    if (system != null && system.Id != current?.Id) AdvanceSystem(system);
                }
        }
        finally { _advancing = false; }
        PublishChanges();
        if (Bootstrapper.Instance != null && Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.Market) &&
            (_lastScheduleLogFrame == int.MinValue || Time.frameCount - _lastScheduleLogFrame >= 120))
        {
            _lastScheduleLogFrame = Time.frameCount;
            Log("SCHEDULE", null, "Systems=" + LastScheduledSystemCount + " | Markets=" + LastProcessedMarketCount + " | EquipmentPasses=" + LastEquipmentUpdateCount);
        }
    }
    private bool BeginAdvance(double seconds)
    {
        if (_committing || _advancing || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || State?.Galaxy == null) return false;
        InitializeSession();
        _advancing = true;
        _changedMarkets.Clear();
        LastScheduledSystemCount = 0; LastProcessedMarketCount = 0; LastEquipmentUpdateCount = 0;
        State.Meta.MarketSimulationSeconds += seconds;
        return true;
    }
    private void AdvanceSystem(StarSystemConfig system)
    {
        if (system == null) return;
        LastScheduledSystemCount++;
        var planets = system.PlanetRefs;
        if (planets != null)
            for (int i = 0; i < planets.Length; i++)
            {
                var planet = planets[i];
                if (planet?.MarketProfile != null) CatchUp(Bind(system.Id, planet.Id, planet.MarketProfile), planet.MarketProfile);
            }
        var stationProfile = StationProfile(system);
        if (stationProfile != null && system.Station != null)
            CatchUp(Bind(system.Id, "station:" + system.Station.Id, stationProfile), stationProfile);
    }
    private void CatchUp(MarketRuntimeData market, MarketProfileConfig profile)
    {
        double now = State.Meta.MarketSimulationSeconds;
        double elapsed = now - market.LastProcessedSimulationSeconds;
        if (elapsed <= 0) return;
        market.LastProcessedSimulationSeconds = now;
        LastProcessedMarketCount++;
        Advance(market, profile, elapsed);
    }
    private void OnResearchCompleted(ResearchCompletedEvent completed)
    {
        if (completed == null || _advancing || _committing) return;
        if (completed.Upgrade == ResearchUpgrade.TradeNewGoods)
        {
            InitializeSession();
            if (State?.Galaxy == null) return;
            _advancing = true; _changedMarkets.Clear();
            _marketLevelOverride = Math.Max(1, completed.Level - 1);
            try
            {
                // Settle all deferred markets at the old research level first.
                for (int i = 0; i < _systems.Count; i++) AdvanceSystem(_systems[i]);
                _marketLevelOverride = null;
                for (int i = 0; i < _systems.Count; i++) AdvanceSystem(_systems[i]);
            }
            finally { _marketLevelOverride = null; _advancing = false; }
            PublishChanges();
            var current = CurrentMarket;
            if (current != null)
                _events?.PublishSafely(new MarketStateChangedEvent(current.SystemId, current.PlanetId), _ => { });
            return;
        }
    }

    private void PublishChanges()
    {
        if (_changedMarkets.Count == 0) return;
        var notifications = _changedMarkets.ToArray();
        _changedMarkets.Clear();
        for (int i = 0; i < notifications.Length; i++)
        {
            bool duplicate = false;
            for (int j = 0; j < i; j++)
                if (notifications[i].SystemId == notifications[j].SystemId && notifications[i].PlanetId == notifications[j].PlanetId) { duplicate = true; break; }
            if (!duplicate) _events?.PublishSafely(notifications[i], _ => { });
        }
    }
    private void Advance(MarketRuntimeData market, MarketProfileConfig profile, double seconds)
    {
        var settings = profile.GetLevel(MarketLevel);
        if (market == null || settings == null || settings.IntervalSeconds <= 0 || float.IsNaN(settings.IntervalSeconds) || float.IsInfinity(settings.IntervalSeconds)) return;
        long revisionBefore = market.Revision;
        double elapsed = market.IntervalRemainder + seconds;
        double intervals = Math.Floor((elapsed + 1e-9) / settings.IntervalSeconds);
        market.IntervalRemainder = Math.Max(0, elapsed - intervals * settings.IntervalSeconds);
        if (intervals > 0)
        {
            foreach (var entry in settings.Items ?? new List<MarketItemEntry>())
                if (entry?.ItemConfig != null)
                {
                    var stock = Stock(market, entry.ItemConfig.Id);
                    if (stock != null) MarketEconomyRules.AdvanceStock(stock, entry, intervals);
                }
            market.Revision++;
            Log("STOCK", market, "Intervals=" + intervals);
        }
        market.ElapsedSeconds += seconds;
        market.EquipmentPendingSeconds += seconds;
        if (!_equipmentDeadlines.TryGetValue(market, out double deadline))
        {
            deadline = NextEquipmentDeadline(market, profile);
            _equipmentDeadlines[market] = deadline;
        }
        if (market.ElapsedSeconds + 1e-9 >= deadline)
        {
            FlushEquipment(market, settings, profile.OfferRefreshIntervalSeconds);
            _equipmentDeadlines[market] = NextEquipmentDeadline(market, profile);
        }
        if (market.Revision != revisionBefore)
            _changedMarkets.Add(new MarketStateChangedEvent(market.SystemId, market.PlanetId));
    }
    public double ClaimOffline(long nowUtcTicks)
    {
        var meta = State?.Meta;
        if (_committing || _advancing || meta == null || nowUtcTicks <= 0 || nowUtcTicks > DateTime.MaxValue.Ticks) return 0;
        long from = Math.Max(meta.LastSaveUtc, meta.MarketOfflineProcessedUtc);
        if (from <= 0 || nowUtcTicks <= from) return 0;
        meta.MarketOfflineProcessedUtc = nowUtcTicks;
        return (nowUtcTicks - from) / (double)TimeSpan.TicksPerSecond;
    }
    public void ProcessOffline(long nowUtcTicks)
    {
        double seconds = ClaimOffline(nowUtcTicks);
        if (seconds <= 0) return;
        AdvanceTime(seconds);
        Log("OFFLINE", null, "Seconds=" + seconds);
        _events?.PublishSafely(new SaveNeedEvent("market_offline_progress"), _ => { });
    }

    private static double NextEquipmentDeadline(MarketRuntimeData market, MarketProfileConfig profile) =>
        market.ElapsedSeconds - market.EquipmentPendingSeconds +
        Math.Max(1, profile.OfferRefreshIntervalSeconds) - market.OfferRefreshRemainder;

    private void FlushEquipment(MarketRuntimeData market, MarketGalaxyLevelSettings settings, int refreshSeconds)
    {
        double seconds = market.EquipmentPendingSeconds;
        if (seconds <= 0) return;
        LastEquipmentUpdateCount++;
        market.EquipmentPendingSeconds = 0;
        int period = Math.Max(1, refreshSeconds);
        double total = market.OfferRefreshRemainder + seconds;
        long cycles = (long)Math.Floor((total + 1e-9) / period);
        market.OfferRefreshRemainder = Math.Max(0, total - cycles * period);
        if (cycles == 0) return;
        // Only the last batch can survive an unattended/offline interval.
        market.OfferRefreshSequence += cycles;
        RefreshOffers(market, market.AppliedProfileId, settings,
            market.ElapsedSeconds - market.OfferRefreshRemainder, period);
    }

    private static bool ValidEquipmentRule(MarketEquipmentCatalogEntry rule, int marketLevel) =>
        !string.IsNullOrEmpty(rule?.EquipmentId) && rule.NewPrice > 0 &&
        (rule.Component != null ? !rule.Component.EnemyOnly : !rule.Weapon.EnemyOnly) &&
        (rule.Component != null ? rule.Component.Level : rule.Weapon.Level) <= marketLevel;

    private bool IsEnemyEquipment(SystemNpcWeaponRuntimeState item) =>
        item != null && ((item.HasLootOrigin && item.LootOriginNpcType == SystemNpcType.Enemy) ||
        (item.IsComponent ? (_config.GetItemConfigById(item.ComponentConfigId) as ShipEquipmentConfig)?.EnemyOnly == true
            : _config.GetWeaponConfigById(item.WeaponConfigId)?.EnemyOnly == true));

    private MarketEquipmentCatalogEntry EquipmentRule(MarketProfileConfig profile, SystemNpcWeaponRuntimeState item) =>
        (MarketEquipmentCatalogEntry)profile.GetLevel(MarketLevel)?.Equipment?.Find(x => x?.Matches(item) == true)
        ?? _config.EquipmentCatalog?.FirstOrDefault(x => x?.Matches(item) == true);
    public int EquipmentPrice(SystemNpcWeaponRuntimeState equipment, bool buy)
    {
        if (equipment == null || !Current(out var market, out var profile)) return 0;
        var listing = market.Equipment.Find(x => x?.Equipment?.InstanceId == equipment.InstanceId);
        int basic = listing?.NewPrice ?? EquipmentBasePrice(profile, equipment);
        // Buying used equipment never costs less than its current sale quote.
        return MarketEconomyRules.EquipmentSellPrice(basic, equipment.CurrentDurability, equipment.MaximumDurability);
    }
    private int EquipmentBasePrice(MarketProfileConfig profile, SystemNpcWeaponRuntimeState item) =>
        EquipmentRule(profile, item)?.NewPrice ?? (item.IsComponent ? GetPrice(item.ComponentConfigId, true) : 0);

    private bool HasEquipmentConfig(SystemNpcWeaponRuntimeState item) =>
        item.IsComponent ? _config.GetItemConfigById(item.ComponentConfigId) is ShipEquipmentConfig
            : _config.GetWeaponConfigById(item.WeaponConfigId) != null;

    public bool TradeEquipment(string instanceId, bool buy, string operationId, long expectedRevision, out string reason, bool warehouse = false)
    {
        reason = "EquipmentUnavailable";
        if (_committing || string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(operationId) || Ship == null || !Current(out var market, out var profile)) return false;
        string intent = (buy ? "equipment_buy:" : "equipment_sell:") + instanceId + (warehouse ? ":storage" : "");
        var receiptMarket = State.Markets.Find(m => m?.Receipts?.Any(x => x != null && x.OperationId == operationId) == true);
        if (receiptMarket != null && !ReferenceEquals(receiptMarket, market)) { reason = "OperationConflict"; return false; }
        var receipt = market.Receipts.Find(x => x != null && x.OperationId == operationId);
        if (receipt != null) { reason = receipt.Intent == intent ? string.Empty : "OperationConflict"; return receipt.Intent == intent; }
        if (expectedRevision >= 0 && expectedRevision != market.Revision) { reason = "MarketChanged"; return false; }
        Ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
        var owned = warehouse ? market.PersonalEquipment : Ship.CargoEquipment;
        var listing = market.Equipment.Find(x => x?.Equipment?.InstanceId == instanceId && x.ExpiresAtSeconds > market.ElapsedSeconds);
        var equipment = buy ? listing?.Equipment : owned.Find(x => x != null && x.InstanceId == instanceId);
        if (equipment == null || (buy && (!listing.Allied || owned.Any(x => x?.InstanceId == instanceId)))) return false;
        var rule = EquipmentRule(profile, equipment);
        if (buy && (IsEnemyEquipment(equipment) || !ValidEquipmentRule(rule, MarketLevel))) return false;
        int basePrice = buy ? listing.NewPrice : EquipmentBasePrice(profile, equipment);
        int price = MarketEconomyRules.EquipmentSellPrice(basePrice, equipment.CurrentDurability, equipment.MaximumDurability);
        if (!HasEquipmentConfig(equipment) || price <= 0) return false;
        if (buy && (State.Player.Credits < price || (!warehouse && !CanCarryEquipment(equipment))))
        { reason = "NotEnoughCreditsOrCargo"; return false; }
        if (!buy && ((long)State.Player.Credits + price > int.MaxValue || market.Equipment.Any(x => x?.Equipment?.InstanceId == instanceId))) return false;
        _committing = true;
        try
        {
            if (buy) { market.Equipment.Remove(listing); owned.Add(equipment); }
            else
            {
                owned.Remove(equipment);
                bool allied = !IsEnemyEquipment(equipment) && (equipment.IsComponent || (rule?.Allied == true &&
                    (!equipment.HasLootOrigin || equipment.LootOriginNpcType == SystemNpcType.Ally)));
                if (allied && ValidEquipmentRule(rule, MarketLevel)) market.Equipment.Add(new MarketEquipmentListing { Equipment = equipment, Allied = true,
                    NewPrice = basePrice, ExpiresAtSeconds = NextOfferExpiry(market, profile) });
            }
            equipment.SlotIndex = -1;
            State.Player.Credits += buy ? -price : price;
            market.Revision++;
            _equipmentDeadlines.Remove(market);
            market.Receipts.Add(new MarketTransactionReceipt { OperationId = operationId, Intent = intent, TotalPrice = price, Quantity = 1 });
            reason = string.Empty;
            Log("EQUIPMENT", market, intent + " | Total=" + price);
            Notify("market_equipment");
            return true;
        }
        finally { _committing = false; }
    }
    private bool CanAddStorage(MarketRuntimeData market, string id, int quantity) => quantity > 0 &&
        _config.GetItemConfigById(id) != null && (long)market.PersonalStorage.GetTradeableQuantity(id) + quantity <= int.MaxValue;
    private bool CanCarryEquipment(SystemNpcWeaponRuntimeState equipment)
    {
        int size = equipment.RolledStats.CargoSize > 0 ? equipment.RolledStats.CargoSize :
            equipment.IsComponent ? _config.GetItemConfigById(equipment.ComponentConfigId)?.CargoSize ?? 1
                : _config.GetWeaponConfigById(equipment.WeaponConfigId)?.CargoSize ?? 1;
        return Ship != null && (long)_inventory.GetUsedCargo() + Math.Max(1, size) <= Ship.CargoCapacity;
    }
    public bool CanTradeEquipment(string id, bool buy, bool warehouse = false)
    {
        if (_committing || Ship == null || !Current(out var market, out var profile)) return false;
        var owned = warehouse ? market.PersonalEquipment : Ship.CargoEquipment;
        var listing = buy ? market.Equipment.Find(x => x?.Equipment?.InstanceId == id && x.Allied && x.ExpiresAtSeconds > market.ElapsedSeconds) : null;
        var item = buy ? listing?.Equipment : owned?.Find(x => x?.InstanceId == id);
        if (item == null || !HasEquipmentConfig(item)) return false;
        if (buy && (IsEnemyEquipment(item) || !ValidEquipmentRule(EquipmentRule(profile, item), MarketLevel))) return false;
        int price = EquipmentPrice(item, buy);
        if (price <= 0) return false;
        return buy ? State.Player.Credits >= price && (warehouse || CanCarryEquipment(item))
            : EquipmentBasePrice(profile, item) > 0 && (long)State.Player.Credits + price <= int.MaxValue;
    }
    public bool CanTransfer(string id, bool equipment, bool toWarehouse, int quantity)
    {
        if (_committing || Ship?.Cargo == null || quantity <= 0 || string.IsNullOrEmpty(id) || !Current(out var market, out _)) return false;
        if (equipment)
        {
            var source = toWarehouse ? Ship.CargoEquipment : market.PersonalEquipment;
            var target = toWarehouse ? market.PersonalEquipment : Ship.CargoEquipment;
            var item = source?.Find(x => x?.InstanceId == id);
            return quantity == 1 && item != null && target?.Any(x => x?.InstanceId == id) != true &&
                (toWarehouse || CanCarryEquipment(item));
        }
        var goods = toWarehouse ? Ship.Cargo : market.PersonalStorage;
        return goods.GetTradeableQuantity(id) >= quantity &&
            (toWarehouse ? CanAddStorage(market, id, quantity) : _inventory.CanAddItem(id, quantity));
    }
    public bool Transfer(string id, bool equipment, bool toWarehouse, int quantity, string operationId, long expectedRevision, out string reason)
    {
        reason = "TransferUnavailable";
        if (_committing || Ship?.Cargo == null || quantity <= 0 || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(operationId) || !Current(out var market, out _)) return false;
        string intent = "transfer:" + (equipment ? "equipment:" : "goods:") + id + ":" + quantity + (toWarehouse ? ":storage" : ":cargo");
        var receiptMarket = State.Markets.Find(m => m?.Receipts?.Any(x => x?.OperationId == operationId) == true);
        if (receiptMarket != null)
        {
            var receipt = receiptMarket.Receipts.Find(x => x?.OperationId == operationId);
            bool same = ReferenceEquals(receiptMarket, market) && receipt.Intent == intent;
            reason = same ? string.Empty : "OperationConflict"; return same;
        }
        if (expectedRevision >= 0 && expectedRevision != market.Revision) { reason = "MarketChanged"; return false; }
        Ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
        var sourceEquipment = toWarehouse ? Ship.CargoEquipment : market.PersonalEquipment;
        var targetEquipment = toWarehouse ? market.PersonalEquipment : Ship.CargoEquipment;
        var item = equipment ? sourceEquipment.Find(x => x?.InstanceId == id) : null;
        var sourceGoods = toWarehouse ? Ship.Cargo : market.PersonalStorage;
        var targetGoods = toWarehouse ? market.PersonalStorage : Ship.Cargo;
        if (equipment)
        {
            if (quantity != 1 || item == null || targetEquipment.Any(x => x?.InstanceId == id)) return false;
            if (!toWarehouse && !CanCarryEquipment(item)) { reason = "NotEnoughCargoSpace"; return false; }
        }
        else
        {
            if (sourceGoods.GetTradeableQuantity(id) < quantity) { reason = "NotEnoughItemsToSell"; return false; }
            if (!(toWarehouse ? CanAddStorage(market, id, quantity) : _inventory.CanAddItem(id, quantity))) { reason = "NotEnoughCargoSpace"; return false; }
        }
        _committing = true;
        try
        {
            if (equipment) { sourceEquipment.Remove(item); targetEquipment.Add(item); item.SlotIndex = -1; }
            else
            {
                bool added = toWarehouse ? targetGoods.AddLootItem(id, quantity) : targetGoods.AddItem(id, quantity, Ship.CargoCapacity);
                if (!added) { reason = "NotEnoughCargoSpace"; return false; }
                if (!sourceGoods.RemoveItem(id, quantity)) { targetGoods.RemoveItem(id, quantity); return false; }
            }
            market.Revision++;
            market.Receipts.Add(new MarketTransactionReceipt { OperationId = operationId, Intent = intent, TotalPrice = 0, Quantity = quantity });
            reason = string.Empty; Log("TRANSFER", market, intent); Notify("personal_storage_transfer"); return true;
        }
        finally { _committing = false; }
    }

    private static int StableHash(string value)
    {
        unchecked { int hash = 17; foreach (char c in value) hash = hash * 31 + c; return hash; }
    }
    private void Notify(string reason)
    {
        // All state and the receipt are committed before any observer can request a save.
        _events?.PublishSafely(new CreditsChangedEvent(State.Player.Credits), _ => { });
        _events?.PublishSafely(new CargoChangedEvent(_inventory.GetUsedCargo()), _ => { });
        _events?.PublishSafely(new SaveNeedEvent(reason), _ => { });
    }
    private static void Log(string action, MarketRuntimeData market, string detail) => AppLog.Info(DebugLogChannel.Market,
        "[PlanetMarket] " + action + " | Planet=" + market?.PlanetId + " | " + detail);
}
