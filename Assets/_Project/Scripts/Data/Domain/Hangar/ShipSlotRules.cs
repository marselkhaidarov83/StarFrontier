using System.Linq;

public static class ShipSlotRules
{
    public static HangarOperationResult CanEquipWeapon(
        ShipRuntimeData shipState,
        ShipConfig shipData,
        WeaponConfig weaponData,
        string weaponId)
    {
        if (shipState == null || shipData == null)
            return HangarOperationResult.Fail(HangarError.ActiveShipMissing);

        if (string.IsNullOrEmpty(weaponId))
            return HangarOperationResult.Fail(HangarError.InvalidWeaponId);

        if (weaponData == null)
            return HangarOperationResult.Fail(HangarError.WeaponNotFound);

        if (shipState.EquippedWeaponIds.Contains(weaponId))
            return HangarOperationResult.Fail(HangarError.AlreadyEquipped);

        if (shipState.EquippedWeaponIds.Count >= shipData.WeaponSlotCount)
            return HangarOperationResult.Fail(HangarError.NoWeaponSlots);

        return HangarOperationResult.Ok();
    }

    public static HangarOperationResult CanUnequipWeapon(
        ShipRuntimeData shipState,
        string weaponId)
    {
        if (shipState == null)
            return HangarOperationResult.Fail(HangarError.ActiveShipMissing);

        if (string.IsNullOrEmpty(weaponId))
            return HangarOperationResult.Fail(HangarError.InvalidWeaponId);

        if (!shipState.EquippedWeaponIds.Contains(weaponId))
            return HangarOperationResult.Fail(HangarError.NotEquipped);

        return HangarOperationResult.Ok();
    }

    public static HangarOperationResult CanEquipModule(
        ShipRuntimeData shipState,
        ShipConfig shipData,
        ModuleConfig moduleData,
        string moduleId)
    {
        if (shipState == null || shipData == null)
            return HangarOperationResult.Fail(HangarError.ActiveShipMissing);

        if (string.IsNullOrEmpty(moduleId))
            return HangarOperationResult.Fail(HangarError.InvalidModuleId);

        if (moduleData == null)
            return HangarOperationResult.Fail(HangarError.ModuleNotFound);

        if (shipState.EquippedModuleIds.Contains(moduleId))
            return HangarOperationResult.Fail(HangarError.AlreadyEquipped);

        if (shipState.EquippedModuleIds.Count >= shipData.ModuleSlotCount)
            return HangarOperationResult.Fail(HangarError.NoModuleSlots);

        return HangarOperationResult.Ok();
    }

    public static HangarOperationResult CanUnequipModule(
        ShipRuntimeData shipState,
        string moduleId)
    {
        if (shipState == null)
            return HangarOperationResult.Fail(HangarError.ActiveShipMissing);

        if (string.IsNullOrEmpty(moduleId))
            return HangarOperationResult.Fail(HangarError.InvalidModuleId);

        if (!shipState.EquippedModuleIds.Contains(moduleId))
            return HangarOperationResult.Fail(HangarError.NotEquipped);

        return HangarOperationResult.Ok();
    }
}
