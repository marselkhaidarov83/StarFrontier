using System;

// Тариф услуги — базовая цена соответствующего товара, за единицу восстановления.
public static class LocationServicePrices
{
    public const string HullRepairItemId = "item_repair_01";
    public const string WeaponRepairItemId = "item_repair_weapon_01";
    public const string FuelItemId = "item_fuel_cells_01";

    public static int Price(IConfigService config, string itemId) =>
        string.IsNullOrEmpty(itemId) ? 0 : Math.Max(0, config?.GetItemConfigById(itemId)?.BasePrice ?? 0);

    public static string ComponentRepairItemId(ShipComponentSlot slot) => slot switch
    {
        ShipComponentSlot.Engine => "item_repair_engine_01",
        ShipComponentSlot.FuelTank => "item_repair_fuel_tank_01",
        ShipComponentSlot.Radar => "item_repair_radar_01",
        ShipComponentSlot.Scanner => "item_repair_scanner_01",
        ShipComponentSlot.RepairDroid => "item_repair_repair_droid_01",
        ShipComponentSlot.Grabber => "item_repair_grabber_01",
        ShipComponentSlot.ShieldGenerator => "item_repair_shield_generator_01",
        _ => null
    };

    public static bool IsServiceItem(string id) =>
        id == FuelItemId || id == HullRepairItemId || id == WeaponRepairItemId ||
        id == ComponentRepairItemId(ShipComponentSlot.Engine) ||
        id == ComponentRepairItemId(ShipComponentSlot.FuelTank) ||
        id == ComponentRepairItemId(ShipComponentSlot.Radar) ||
        id == ComponentRepairItemId(ShipComponentSlot.Scanner) ||
        id == ComponentRepairItemId(ShipComponentSlot.RepairDroid) ||
        id == ComponentRepairItemId(ShipComponentSlot.Grabber) ||
        id == ComponentRepairItemId(ShipComponentSlot.ShieldGenerator);
}
