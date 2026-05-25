using System;
using UnityEngine;

public class HangarService : IHangarService
{
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;
    private readonly IGameSessionService _gameSessionService;
    private readonly ISaveService _saveService;
    private readonly ShipStatCalculator _statCalculator;

    public HangarService()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _statCalculator = new ShipStatCalculator();
    }

    public ShipRuntimeData GetActiveShipState()
    {
        if (_gameSessionService.CurrentSave.PlayerProfile.PlayerShipState == null)
            return null;

        return _gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip();
    }

    public ShipConfig GetActiveShipData()
    {
        var activeShip = GetActiveShipState();

        if (activeShip == null)
            return null;

        return _configService.GetShipConfigById(activeShip.ShipConfigId);
        // _configService.TryGetById(activeShip.ShipId, out ShipData shipData);
        // return shipData;
    }

    public ShipStats GetActiveShipStats()
    {
        var activeShip = GetActiveShipState();
        var shipData = GetActiveShipData();

        if (activeShip == null || shipData == null)
            return null;

        return _statCalculator.Calculate(shipData, activeShip.EquippedModuleIds);
    }

    public HangarOperationResult SwitchShip(string shipId)
    {
        if (_gameSessionService.CurrentSave.PlayerProfile.PlayerShipState == null)
            return HangarOperationResult.Fail(HangarError.ActiveShipMissing);

        if (string.IsNullOrEmpty(shipId))
            return HangarOperationResult.Fail(HangarError.ShipNotFound);

        ShipRuntimeData shipRuntime = _gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetOwnedShip(shipId);
        // if (!_gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.OwnsShip(shipId))
        if (shipRuntime == null)
            return HangarOperationResult.Fail(HangarError.ShipNotOwned);

        // ShipConfig shipData = _configService.GetShipConfigById(shipId);
        ShipConfig shipData = _configService.GetShipConfigById(shipRuntime.ShipConfigId);
        // if (!_configService.TryGetById(shipId, out ShipData shipData))
        if (shipData == null)
            return HangarOperationResult.Fail(HangarError.ShipNotFound);

        _gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.ActiveShipId = shipId;

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

        Debug.Log("[HangarService] EquipWeapon " + weaponId);
        WeaponConfig weaponData = _configService.GetWeaponConfigById(weaponId);
        // if (!_configService.TryGetById(weaponId, out WeaponData weaponData))
            // weaponData = null;
        Debug.Log("[HangarService] weaponData " + weaponData);

        var result = ShipSlotRules.CanEquipWeapon(
            activeShip,
            shipData,
            weaponData,
            weaponId
        );
        Debug.Log("[HangarService] CanEquipWeapon " + result.Error);

        if (!result.Success)
            return result;

        activeShip.EquippedWeaponIds.Add(weaponId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));

        SaveAfterSuccessfulHangarOperation("EquipWeapon");
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult UnequipWeapon(string weaponId)
    {
        var activeShip = GetActiveShipState();

        var result = ShipSlotRules.CanUnequipWeapon(
            activeShip,
            weaponId
        );

        if (!result.Success)
            return result;

        activeShip.EquippedWeaponIds.Remove(weaponId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));

        SaveAfterSuccessfulHangarOperation("UnequipWeapon");
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult EquipModule(string moduleId)
    {
        var activeShip = GetActiveShipState();
        var shipData = GetActiveShipData();

        ModuleConfig moduleData = _configService.GetModuleConfigById(moduleId);
        // if (!_configService.TryGetById(moduleId, out ModuleData moduleData))
        //     moduleData = null;

        var result = ShipSlotRules.CanEquipModule(
            activeShip,
            shipData,
            moduleData,
            moduleId
        );

        if (!result.Success)
            return result;

        activeShip.EquippedModuleIds.Add(moduleId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));
        _eventBus.Publish(new ShipStatsChangedEvent(activeShip.ShipId));

        SaveAfterSuccessfulHangarOperation("EquipModule");
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult UnequipModule(string moduleId)
    {
        var activeShip = GetActiveShipState();

        var result = ShipSlotRules.CanUnequipModule(
            activeShip,
            moduleId
        );

        if (!result.Success)
            return result;

        activeShip.EquippedModuleIds.Remove(moduleId);

        _eventBus.Publish(new ShipEquipmentChangedEvent(activeShip.ShipId));
        _eventBus.Publish(new ShipStatsChangedEvent(activeShip.ShipId));

        SaveAfterSuccessfulHangarOperation("UnequipModule");
        return HangarOperationResult.Ok();
    }

    public HangarOperationResult RepairActiveShip()
    {
        var activeShip = GetActiveShipState();
        var shipData = GetActiveShipData();

        if (activeShip == null || shipData == null)
            return HangarOperationResult.Fail(HangarError.ActiveShipMissing);

        if (activeShip.CurrentHull >= shipData.BaseHull)
            return HangarOperationResult.Ok();

        activeShip.CurrentHull = shipData.BaseHull;

        _eventBus.Publish(new ShipRepairedEvent(activeShip.ShipId));
        _eventBus.Publish(new ShipStatsChangedEvent(activeShip.ShipId));

        SaveAfterSuccessfulHangarOperation("RepairActiveShip");
        return HangarOperationResult.Ok();
    }

    private void SaveAfterSuccessfulHangarOperation(string operationName)
    {
        if (_saveService == null)
        {
            UnityEngine.Debug.LogWarning($"[HangarService] SaveService is missing. Operation was not saved: {operationName}");
            return;
        }

        try
        {
            _saveService.Save();
            UnityEngine.Debug.Log($"[HangarService] Autosaved after: {operationName}");
        }
        catch (System.Exception exception)
        {
            UnityEngine.Debug.LogError($"[HangarService] Autosave failed after {operationName}: {exception.Message}");
        }
    }    
}