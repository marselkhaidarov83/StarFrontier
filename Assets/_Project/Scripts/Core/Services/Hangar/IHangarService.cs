public enum HangarRepairScope
{
    Installed,
    FullShip,
    Inventory
}

public interface IHangarService
{
    ShipRuntimeData GetActiveShipState();
    AllyConfig GetActiveShipData();
    ShipStats GetActiveShipStats();

    HangarOperationResult SwitchShip(string shipId);

    HangarOperationResult EquipWeapon(string weaponId);
    HangarOperationResult UnequipWeapon(string weaponId);

    HangarOperationResult EquipModule(string moduleId);
    HangarOperationResult UnequipModule(string moduleId);

    bool TryChangeLoadout(int slot, string itemId, bool warehouse, out string reason);

    bool TryRemoveAllEquipment(bool warehouse, out string reason);

    bool TryDebugDamageSelection(int slot, string itemId, bool warehouse, out string reason);

    long GetRepairPrice(HangarRepairScope scope, int slot, string itemId = null, bool warehouse = false);
    bool TryRepair(HangarRepairScope scope, int slot, string itemId, bool warehouse, long quotedPrice, out string reason);
}