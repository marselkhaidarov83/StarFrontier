using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HangarScreenController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private Button repairButton;
    [SerializeField] private Button backButton;
    [SerializeField] private bool useLegacyLayout;

    private sealed class InventoryChoice
    {
        public string Id, Title, Info;
        public Sprite Icon;
        public bool CanInstall;
        public (int Current, int Maximum) Durability;
    }

    private readonly List<InventoryChoice> _choices = new List<InventoryChoice>();
    private IHangarService _hangar;
    private IPlanetMarketService _market;
    private IInventoryService _inventory;
    private IConfigService _config;
    private IGameSessionService _session;
    private PlanetGovernmentMissionPresenter _government;
    private HangarPanelView2A _view;
    private int _slot;
    private bool _warehouse, _busy;
    private string _choice, _message, _context;
    private long _installedRepairPrice, _fullRepairPrice, _inventoryRepairPrice;

    private void Awake()
    {
        var registry = Bootstrapper.Instance.ServiceRegistry;
        _hangar = registry.Get<IHangarService>();
        _market = registry.Get<IPlanetMarketService>();
        _inventory = registry.Get<IInventoryService>();
        _config = registry.Get<IConfigService>();
        _session = registry.Get<IGameSessionService>();
        backButton?.onClick.AddListener(Close);

        var legacy = GetComponent<HangarPanelView2A>() ?? gameObject.AddComponent<HangarPanelView2A>();
        BuildView(legacy, repairButton, false);
        if (useLegacyLayout)
        {
            _government = FindFirstObjectByType<PlanetGovernmentMissionPresenter>(FindObjectsInactive.Include);
            _view = legacy;
            return;
        }

        legacy.HideLayout();
        var stacked = ResearchPanelUi2A.Rect(transform, "StackedHangar");
        _view = stacked.gameObject.AddComponent<HangarPanelView2A>();
        BuildView(_view, null, true);
    }

    private void BuildView(HangarPanelView2A view, Button repair, bool stacked)
    {
        view.BuildLoadout(shipNameText, repair, SelectSlot, SelectStorage, SelectChoice,
            () => ChangeEquipment(_choice), () => ChangeEquipment(null),
            () => Repair(HangarRepairScope.Installed, _installedRepairPrice),
            () => Repair(HangarRepairScope.FullShip, _fullRepairPrice),
            () => Repair(HangarRepairScope.Inventory, _inventoryRepairPrice), RemoveAllEquipment, stacked);
    }

    private void OnEnable()
    {
        _view?.ResetCardSelections();
        _message = string.Empty;
        Refresh();
        AppLog.Info(DebugLogChannel.Hangar, typeof(HangarScreenController), "Shared repair/hangar panel opened");
    }

    public void Open()
    {
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        _view?.CloseEquipmentModal();
        root.SetActive(false);
    }

    private void SelectSlot(int slot)
    {
        _slot = slot;
        _choice = null;
        _message = string.Empty;
        Refresh();
        _view.ResetLoadoutInventoryScroll();
    }

    private void SelectStorage(bool warehouse)
    {
        _warehouse = warehouse;
        _choice = null;
        _message = string.Empty;
        Refresh();
        _view.ResetLoadoutInventoryScroll();
    }

    private void SelectChoice(int index)
    {
        if (index < 0 || index >= _choices.Count) return;
        _choice = _choices[index].Id;
        _message = string.Empty;
        Refresh();
    }

    private void ChangeEquipment(string id)
    {
        if (_busy || !isActiveAndEnabled) return;
        _busy = true;
        try
        {
            if (_hangar.TryChangeLoadout(_slot, id, _warehouse, out _message)) _choice = null;
        }
        finally
        {
            _busy = false;
            Refresh();
        }
    }

    private void RemoveAllEquipment()
    {
        if (_busy || !isActiveAndEnabled) return;
        _busy = true;
        try
        {
            if (_hangar.TryRemoveAllEquipment(_warehouse, out _message)) _choice = null;
        }
        finally
        {
            _busy = false;
            Refresh();
        }
    }

    [ContextMenu("STAR FRONTIER/Ангар/Нанести 10 ед. урона выбранному оборудованию")]
    private void DebugDamageSelectedEquipment()
    {
        if (!DeveloperModeGate.CanRunExplicitCommand(Application.isPlaying, Application.isEditor, DeveloperModeGate.IsEnabled) ||
            !isActiveAndEnabled || _busy || _hangar == null)
        {
            AppLog.Warning(DebugLogChannel.Hangar, "Открой ангар в Play Mode и выбери оборудование.");
            return;
        }

        _busy = true;
        try
        {
            _hangar.TryDebugDamageSelection(_slot, _choice, _warehouse, out _message);
        }
        finally
        {
            _busy = false;
            Refresh();
        }
    }

    private void Repair(HangarRepairScope scope, long price)
    {
        if (_busy || !isActiveAndEnabled || price <= 0) return;
        _busy = true;
        try
        {
            bool repaired = _hangar.TryRepair(scope, _slot, _choice, _warehouse, price, out string reason);
            _message = repaired ? "Ремонт выполнен." : reason;
        }
        finally
        {
            _busy = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_view == null || !_view.IsReady) return;
        var player = _session.State?.Player;
        var station = StationLocationRules.GetDockedStation(
            player, _config.GetStarSystemConfigById(player?.CurrentSystemId));
        if (useLegacyLayout)
        {
            var planet = station == null ? _config.GetPlanetConfigById(player?.CurrentPlanetId) : null;
            var fallback = _government != null ? _government.CurrentLocationIllustration : null;
            _view.SetHeader(station?.StationTypeConfig?.RepairIllustration ??
                planet?.PlanetTypeConfig?.RepairIllustration ?? fallback);
        }

        var ship = _hangar.GetActiveShipState();
        var hull = _hangar.GetActiveShipData();
        var stats = _hangar.GetActiveShipStats();
        var market = _market.CurrentMarket;
        string context = player?.CurrentSystemId + "|" + player?.CurrentPlanetId + "|" + player?.CurrentStationId + "|" + ship?.ShipId;
        if (_context != context)
        {
            _context = context;
            _slot = ShipLoadoutRules.HullSlot;
            _choice = null;
            _warehouse = false;
        }
        bool ready = ship?.HasModularLoadout == true && hull != null && market != null && stats != null;
        if (!SlotAvailable(hull, _slot))
        {
            _slot = ShipLoadoutRules.HullSlot;
            _choice = null;
        }

        int usedCargo = ready ? _inventory.GetUsedCargo() : 0;
        RenderSlots(ship, hull, ready, usedCargo);
        BuildChoices(ship, market, ready);
        var selectedChoice = _choices.Find(item => item.Id == _choice);
        if (selectedChoice == null) _choice = null;
        _view.SetInventory(_warehouse, station != null ? "Станция" : "Планета", _choices.Count, ready);
        for (int i = 0; i < _choices.Count; i++)
        {
            var item = _choices[i];
            _view.SetChoice(i, item.Title, item.Icon, item.Id == _choice, item.Info, item.Id, item.Durability);
        }

        bool occupied = IsOccupied(ship, _slot);
        string summary = stats == null ? "Корабль недоступен" :
            $"Корпус {ship.CurrentHull}/{stats.MaxHull}    Поле {ship.CurrentShield}/{stats.MaxShield}    Скорость {stats.Speed:0.##}\n" +
            $"Топливо {ship.CurrentFuel}/{ship.FuelCapacity}";
        bool canRemoveAll = ready && (ship.InstalledComponents.Exists(item => item != null) ||
            ship.InstalledWeapons.Exists(item => item != null));
        _installedRepairPrice = ready ? _hangar.GetRepairPrice(HangarRepairScope.Installed, _slot) : 0;
        _fullRepairPrice = ready ? _hangar.GetRepairPrice(HangarRepairScope.FullShip, _slot) : 0;
        _inventoryRepairPrice = ready && selectedChoice != null
            ? _hangar.GetRepairPrice(HangarRepairScope.Inventory, _slot, _choice, _warehouse) : 0;
        _view.SetLoadoutActions(occupied, ready && selectedChoice?.CanInstall == true,
            ready && occupied && _slot != ShipLoadoutRules.HullSlot, summary, _message ?? string.Empty,
            _installedRepairPrice, _fullRepairPrice, _inventoryRepairPrice, player?.Credits ?? 0, canRemoveAll);
    }

    private void RenderSlots(ShipRuntimeData ship, AllyConfig hull, bool ready, int usedCargo)
    {
        _view.SetSlot(ShipLoadoutRules.HullSlot, hull == null ? "Корпус" : string.Empty,
            hull != null ? hull.CombatSprite ?? hull.Icon : null, ready, _slot == ShipLoadoutRules.HullSlot,
            ship != null && hull != null
                ? WithDescription(hull.Description, $"Вместимость трюма {usedCargo} / {ship.CargoCapacity}")
                : null,
            ship?.InstalledHull?.InstanceId, equipmentName: hull?.DisplayName, durability: (ship?.CurrentHull ?? 0, ship?.HullCapacity ?? 0));

        for (int i = ShipLoadoutRules.FirstComponentSlot; i < ShipLoadoutRules.FirstWeaponSlot; i++)
        {
            bool allowed = SlotAvailable(hull, i);
            var instance = ShipLoadoutRules.Component(ship, (ShipComponentSlot)(i - ShipLoadoutRules.FirstComponentSlot));
            var component = _config.GetItemConfigById(instance?.ComponentConfigId) as ShipComponentConfig;
            _view.SetSlot(i, instance == null ? SlotName(i) : string.Empty, component?.Icon, ready && allowed, _slot == i,
                instance != null ? ComponentInfo(instance, component, ship.CurrentFuel) : null,
                instance?.InstanceId, empty: instance == null, equipmentName: component?.DisplayName ?? SlotName(i),
                durability: Durability(instance));
        }

        for (int i = ShipLoadoutRules.FirstWeaponSlot; i < ShipLoadoutRules.TotalSlots; i++)
        {
            bool allowed = SlotAvailable(hull, i);
            var weapon = ShipLoadoutRules.Weapon(ship, i - ShipLoadoutRules.FirstWeaponSlot);
            var config = _config.GetWeaponConfigById(weapon?.WeaponConfigId);
            _view.SetSlot(i, weapon == null ? SlotName(i) : string.Empty, config?.Icon, ready && allowed, _slot == i,
                weapon != null ? WeaponInfo(weapon, config) : null, weapon?.InstanceId, empty: weapon == null, equipmentName: config?.DisplayName ?? SlotName(i),
                durability: Durability(weapon));
        }
    }

    private void BuildChoices(ShipRuntimeData ship, MarketRuntimeData market, bool ready)
    {
        _choices.Clear();
        if (!ready) return;
        if (_slot == ShipLoadoutRules.HullSlot)
        {
            if (!_warehouse) return;
            foreach (var hull in market.PersonalHulls)
            {
                var config = _market.GetHullConfig(hull?.ConfigId);
                if (hull == null || config?.Hull == null) continue;
                AddChoice(hull.InstanceId, config.DisplayName, config.Icon,
                    WithDescription(config.Hull.Description, $"Вместимость трюма {config.Hull.BaseCargoCapacity}"),
                    (hull.CurrentDurability, hull.MaximumDurability), hull.CurrentDurability > 0);
            }
        }
        else if (_slot >= ShipLoadoutRules.FirstWeaponSlot)
        {
            foreach (var weapon in _warehouse ? market.PersonalEquipment : ship.CargoEquipment)
            {
                if (weapon == null || weapon.IsComponent) continue;
                var config = _config.GetWeaponConfigById(weapon.WeaponConfigId);
                if (config != null)
                    AddChoice(weapon.InstanceId, config.DisplayName, config.Icon, WeaponInfo(weapon, config), Durability(weapon));
            }
        }
        else
        {
            var slot = (ShipComponentSlot)(_slot - ShipLoadoutRules.FirstComponentSlot);
            foreach (var item in _warehouse ? market.PersonalEquipment : ship.CargoEquipment)
            {
                if (item == null || !item.IsComponent ||
                    _config.GetItemConfigById(item.ComponentConfigId) is not ShipComponentConfig component || component.Slot != slot)
                    continue;
                AddChoice(item.InstanceId, component.DisplayName, component.Icon, ComponentInfo(item, component), Durability(item));
            }
        }
    }

    private void AddChoice(string id, string title, Sprite icon, string info, (int Current, int Maximum) durability, bool canInstall = true)
    {
        _choices.Add(new InventoryChoice { Id = id, Title = title, Icon = icon, Info = info, Durability = durability, CanInstall = canInstall });
    }

    private static (int Current, int Maximum) Durability(SystemNpcWeaponRuntimeState item) =>
        (item?.CurrentDurability ?? 0, item?.MaximumDurability ?? 0);

    internal static string WithDescription(string description, string details) =>
        string.IsNullOrWhiteSpace(description) ? details : description.Trim() + "\n\n" + details;

    internal static string CargoSpaceLine(int size) => $"Занимает места в трюме: {Mathf.Max(1, size)}";

    internal static string ComponentInfo(SystemNpcWeaponRuntimeState item, ShipComponentConfig component, int? installedFuel = null)
    {
        string details = component?.Description;
        if (component != null && component.Slot == ShipComponentSlot.Engine)
        {
            string range = component.EngineRange > 0 ? component.EngineRange.ToString("0.##") : "—";
            details = $"Скорость: {component.Speed:0.##}\nДальность: {range}";
        }

        if (component != null && component.Slot == ShipComponentSlot.FuelTank)
            details = $"Топливо: {installedFuel ?? item.StoredFuel} / {component.FuelCapacity}";

        int cargoSize = item.RolledStats.CargoSize > 0 ? item.RolledStats.CargoSize : component?.CargoSize ?? 1;
        string cargo = CargoSpaceLine(cargoSize);
        if (component != null && (component.Slot == ShipComponentSlot.Engine || component.Slot == ShipComponentSlot.FuelTank))
            return WithDescription(component.Description, details + "\n" + cargo);
        return WithDescription(details, cargo);
    }

    internal static string WeaponInfo(SystemNpcWeaponRuntimeState weapon, WeaponConfig config)
    {
        var stats = weapon.RolledStats;
        int cargo = stats.CargoSize > 0 ? stats.CargoSize : config?.CargoSize ?? 1;
        var lines = MarketScreenRules2A.WeaponDetails(stats.Range, stats.Damage, stats.ShotCount, cargo);
        return WithDescription(config?.Description, lines.Range + "\n" + lines.Damage + "\n" + lines.Shots + "\n" + CargoSpaceLine(cargo));
    }

    private static bool SlotAvailable(AllyConfig hull, int index)
    {
        if (index == ShipLoadoutRules.HullSlot) return hull != null;
        if (index >= ShipLoadoutRules.FirstWeaponSlot)
            return index - ShipLoadoutRules.FirstWeaponSlot < ShipLoadoutRules.AvailableWeapons(hull);
        return ShipLoadoutRules.Supports(hull, (ShipComponentSlot)(index - ShipLoadoutRules.FirstComponentSlot));
    }

    private static bool IsOccupied(ShipRuntimeData ship, int index)
    {
        if (index == ShipLoadoutRules.HullSlot) return ship != null;
        if (index >= ShipLoadoutRules.FirstWeaponSlot)
            return ShipLoadoutRules.Weapon(ship, index - ShipLoadoutRules.FirstWeaponSlot) != null;
        return !string.IsNullOrEmpty(ShipLoadoutRules.ComponentId(ship, (ShipComponentSlot)(index - ShipLoadoutRules.FirstComponentSlot)));
    }

    private static string SlotName(int index)
    {
        if (index == ShipLoadoutRules.HullSlot) return "Корпус";
        if (index >= ShipLoadoutRules.FirstWeaponSlot)
            return "Оружие";
        return ((ShipComponentSlot)(index - ShipLoadoutRules.FirstComponentSlot)) switch
        {
            ShipComponentSlot.Engine => "Двигатель",
            ShipComponentSlot.FuelTank => "Топливный бак",
            ShipComponentSlot.Radar => "Радар",
            ShipComponentSlot.Scanner => "Сканер",
            ShipComponentSlot.RepairDroid => "Ремонтный дроид",
            ShipComponentSlot.Grabber => "Захват",
            ShipComponentSlot.ShieldGenerator => "Генератор поля",
            _ => string.Empty
        };
    }
}
