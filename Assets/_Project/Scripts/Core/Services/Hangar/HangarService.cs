using System;
using UnityEngine;

public partial class HangarService : IHangarService
{
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;
    private readonly IGameSessionService _gameSessionService;
    private readonly ShipStatCalculator _statCalculator;
    private readonly IPlanetMarketService _market;
    private readonly IInventoryService _inventory;

    public HangarService() : this(
        Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IPlanetMarketService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IInventoryService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>())
    {
    }

    public HangarService(IGameSessionService session, IConfigService config, IPlanetMarketService market,
        IInventoryService inventory, SimpleEventBus events)
    {
        _gameSessionService = session;
        _configService = config;
        _market = market;
        _inventory = inventory;
        _eventBus = events;
        _statCalculator = new ShipStatCalculator(config);
    }

    public ShipRuntimeData GetActiveShipState()
    {
        if (_gameSessionService.State?.Player?.PlayerShipState == null)
            return null;

        return _gameSessionService.State.Player.PlayerShipState.GetActiveShip();
    }

    public AllyConfig GetActiveShipData()
    {
        var activeShip = GetActiveShipState();

        if (activeShip == null)
            return null;

        return _configService.GetAllyConfigById(activeShip.AllyConfigId);
    }

    public ShipStats GetActiveShipStats()
    {
        var activeShip = GetActiveShipState();
        var shipData = GetActiveShipData();

        if (activeShip == null || shipData == null)
            return null;

        var stats = _statCalculator.Calculate(shipData, activeShip.EquippedModuleIds);
        if (!activeShip.HasModularLoadout) return stats;

        stats.MaxHull = Mathf.Max(1, activeShip.HullCapacity);
        stats.CargoCapacity = Mathf.Max(0, activeShip.CargoCapacity);
        var shieldItem = ShipLoadoutRules.Component(activeShip, ShipComponentSlot.ShieldGenerator);
        var shield = GetComponent(shieldItem?.ComponentConfigId);
        stats.MaxShield = shieldItem?.GetComponentStats(shield)?.ShieldCapacity ?? 0;
        stats.Speed = ShipStatCalculator.GetEngineSpeed(activeShip, _configService);
        return stats;
    }

    public HangarOperationResult SwitchShip(string shipId)
    {
        if (_gameSessionService.State?.Player?.PlayerShipState == null)
            return HangarOperationResult.Fail(HangarError.ActiveShipMissing);

        if (string.IsNullOrEmpty(shipId))
            return HangarOperationResult.Fail(HangarError.ShipNotFound);

        ShipRuntimeData shipRuntime = _gameSessionService.State.Player.PlayerShipState.GetOwnedShip(shipId);

        if (shipRuntime == null)
            return HangarOperationResult.Fail(HangarError.ShipNotOwned);

        AllyConfig shipData =
            _configService.GetAllyConfigById(shipRuntime.AllyConfigId);

        if (shipData == null)
            return HangarOperationResult.Fail(HangarError.ShipNotFound);

        _gameSessionService.State.Player.PlayerShipState.ActiveShipId = shipId;

        _eventBus.Publish(new ActiveShipChangedEvent(shipId));
        _eventBus.Publish(new ShipStatsChangedEvent(shipId));
        _eventBus.Publish(new ShipEquipmentChangedEvent(shipId));

        SaveAfterSuccessfulHangarOperation("SwitchShip");
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult EquipWeapon(string weaponId)
    {
        var activeShip = GetActiveShipState();
        var shipData = GetActiveShipData();

        AppLog.Info(DebugLogChannel.Hangar,
            typeof(HangarService),
            "EquipWeapon " + weaponId);

        if (activeShip?.HasModularLoadout == true)
        {
            for (int i = 0; i < ShipLoadoutRules.AvailableWeapons(shipData); i++)
            {
                if (ShipLoadoutRules.Weapon(activeShip, i) != null) continue;
                var item = activeShip.CargoEquipment?.Find(x => x?.WeaponConfigId == weaponId);
                return item != null && TryChangeLoadout(ShipLoadoutRules.FirstWeaponSlot + i, item.InstanceId, false, out _)
                    ? HangarOperationResult.Ok() : HangarOperationResult.Fail(HangarError.WeaponNotFound);
            }
            return HangarOperationResult.Fail(HangarError.NoWeaponSlots);
        }

        WeaponConfig weaponData = _configService.GetWeaponConfigById(weaponId);

        AppLog.Info(DebugLogChannel.Hangar,
            typeof(HangarService),
            "weaponData " + weaponData);

        var result = ShipSlotRules.CanEquipWeapon(
            activeShip,
            shipData,
            weaponData,
            weaponId);

        AppLog.Info(DebugLogChannel.Hangar,
            typeof(HangarService),
            "CanEquipWeapon " + result.Error);

        if (!result.Success)
        {
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.ShipUpgrade, false,
                result.Error.ToString(), weaponId);
            return result;
        }

        if (!FitsCargo(weaponData.CargoSize, activeShip.CargoCapacity, activeShip.CargoCapacity))
            return HangarOperationResult.Fail(HangarError.NotEnoughCargoSpace);

        activeShip.EquippedWeaponIds.Add(weaponId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));

        PublishLoadoutEvent(new CargoChangedEvent(_inventory.GetUsedCargo()));
        SaveAfterSuccessfulHangarOperation("EquipWeapon");
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.ShipUpgrade, true, contentId: weaponId);
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult UnequipWeapon(string weaponId)
    {
        var activeShip = GetActiveShipState();

        if (activeShip?.HasModularLoadout == true)
        {
            var item = activeShip.InstalledWeapons?.Find(x => x?.WeaponConfigId == weaponId);
            return item != null && TryChangeLoadout(ShipLoadoutRules.FirstWeaponSlot + item.SlotIndex, null, false, out _)
                ? HangarOperationResult.Ok() : HangarOperationResult.Fail(HangarError.WeaponNotFound);
        }

        var result = ShipSlotRules.CanUnequipWeapon(
            activeShip,
            weaponId);

        if (!result.Success)
            return result;

        activeShip.EquippedWeaponIds.Remove(weaponId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));

        PublishLoadoutEvent(new CargoChangedEvent(_inventory.GetUsedCargo()));
        SaveAfterSuccessfulHangarOperation("UnequipWeapon");
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult EquipModule(string moduleId)
    {
        var activeShip = GetActiveShipState();
        var shipData = GetActiveShipData();

        ModuleConfig moduleData =
            _configService.GetModuleConfigById(moduleId);

        var result = ShipSlotRules.CanEquipModule(
            activeShip,
            shipData,
            moduleData,
            moduleId);

        if (!result.Success)
        {
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.ShipUpgrade, false,
                result.Error.ToString(), moduleId);
            return result;
        }

        if (!FitsCargo(moduleData.CargoSize, activeShip.CargoCapacity, activeShip.CargoCapacity))
            return HangarOperationResult.Fail(HangarError.NotEnoughCargoSpace);

        activeShip.EquippedModuleIds.Add(moduleId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));
        _eventBus.Publish(new ShipStatsChangedEvent(activeShip.ShipId));

        PublishLoadoutEvent(new CargoChangedEvent(_inventory.GetUsedCargo()));
        SaveAfterSuccessfulHangarOperation("EquipModule");
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.ShipUpgrade, true, contentId: moduleId);
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult UnequipModule(string moduleId)
    {
        var activeShip = GetActiveShipState();

        var result = ShipSlotRules.CanUnequipModule(
            activeShip,
            moduleId);

        if (!result.Success)
            return result;

        activeShip.EquippedModuleIds.Remove(moduleId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));
        _eventBus.Publish(new ShipStatsChangedEvent(activeShip.ShipId));

        PublishLoadoutEvent(new CargoChangedEvent(_inventory.GetUsedCargo()));
        SaveAfterSuccessfulHangarOperation("UnequipModule");
        return HangarOperationResult.Ok();
    }

    private void SaveAfterSuccessfulHangarOperation(string operationName)
    {
        try
        {
            _eventBus.Publish(new SaveNeedEvent());
            AppLog.Info(DebugLogChannel.Hangar,
                typeof(HangarService),
                $"Autosaved after: {operationName}");
        }
        catch (Exception exception)
        {
            AppLog.Error(DebugLogChannel.Hangar,
                typeof(HangarService),
                $"Autosave failed after {operationName}: {exception.Message}");
        }
    }
}
