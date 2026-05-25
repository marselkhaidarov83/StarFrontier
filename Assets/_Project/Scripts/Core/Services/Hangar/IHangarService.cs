public interface IHangarService
{
    ShipRuntimeData GetActiveShipState();
    ShipConfig GetActiveShipData();
    ShipStats GetActiveShipStats();

    HangarOperationResult SwitchShip(string shipId);

    HangarOperationResult EquipWeapon(string weaponId);
    HangarOperationResult UnequipWeapon(string weaponId);

    HangarOperationResult EquipModule(string moduleId);
    HangarOperationResult UnequipModule(string moduleId);

    HangarOperationResult RepairActiveShip();
}