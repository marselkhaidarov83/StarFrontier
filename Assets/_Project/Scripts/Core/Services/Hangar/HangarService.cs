using System;
using UnityEngine;

public class HangarService : IHangarService
{
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;
    private readonly IGameSessionService _gameSessionService;
    private readonly ShipStatCalculator _statCalculator;

    public HangarService()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _statCalculator = new ShipStatCalculator();
    }

    public ShipRuntimeData GetActiveShipState()
    {
        if (_gameSessionService.State.Player.PlayerShipState == null)
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

        return _statCalculator.Calculate(shipData, activeShip.EquippedModuleIds);
    }

    public HangarOperationResult SwitchShip(string shipId)
    {
        if (_gameSessionService.State.Player.PlayerShipState == null)
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

        Debug.Log("[HangarService] EquipWeapon " + weaponId);

        WeaponConfig weaponData = _configService.GetWeaponConfigById(weaponId);

        Debug.Log("[HangarService] weaponData " + weaponData);

        var result = ShipSlotRules.CanEquipWeapon(
            activeShip,
            shipData,
            weaponData,
            weaponId);

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
            weaponId);

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

        ModuleConfig moduleData =
            _configService.GetModuleConfigById(moduleId);

        var result = ShipSlotRules.CanEquipModule(
            activeShip,
            shipData,
            moduleData,
            moduleId);

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
            moduleId);

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
        try
        {
            _eventBus.Publish(new SaveNeedEvent());
            Debug.Log($"[HangarService] Autosaved after: {operationName}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[HangarService] Autosave failed after {operationName}: {exception.Message}");
        }
    }
}