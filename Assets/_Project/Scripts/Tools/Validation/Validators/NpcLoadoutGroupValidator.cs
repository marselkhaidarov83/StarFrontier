using System;
using System.Collections.Generic;

public static class NpcLoadoutGroupValidator
{
    public static List<ValidationIssue> Validate(BaseConfig actor, int level, bool enemy,
        HullGroupConfig hullGroup, EquipmentGroupConfig equipmentGroup)
    {
        var issues = new List<ValidationIssue>();
        void Error(string message) => issues.Add(new ValidationIssue(ValidationSeverity.Error, message, actor));
        if (hullGroup == null || equipmentGroup == null)
        {
            Error("Hull and equipment groups are required.");
            return issues;
        }
        if (hullGroup.Level != level || equipmentGroup.Level != level) Error("Loadout group levels must match the NPC level.");
        if (hullGroup.HullConfigs == null || hullGroup.HullConfigs.Count == 0) Error("Hull group is empty.");
        var available = new HashSet<ShipComponentSlot>();
        var slots = new HashSet<ShipComponentSlot>();
        if (equipmentGroup.SlotGroups != null)
            foreach (EquipmentSlotGroup group in equipmentGroup.SlotGroups)
            {
                if (group == null || !slots.Add(group.Slot)) { Error("Equipment slot is empty or duplicated."); continue; }
                int required = enemy ? 1 : 3;
                if (group.Equipment == null || group.Equipment.Count != required) Error($"Slot {group.Slot} must contain {required} variants.");
                var ids = new HashSet<string>();
                if (group.Equipment != null)
                    foreach (ShipEquipmentConfig item in group.Equipment)
                    {
                        if (item == null || item.Slot != group.Slot || item.Level != Math.Max(1, level) ||
                            item.EnemyOnly != enemy || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
                            Error($"Invalid variant in slot {group.Slot}.");
                        else available.Add(group.Slot);
                    }
            }
        if (hullGroup.HullConfigs != null)
            foreach (HullConfig hull in hullGroup.HullConfigs)
            {
                if (hull == null) { Error("Hull group contains an empty hull."); continue; }
                if (hull.Level != level || hull.EnemyOnly != enemy ||
                    (enemy ? hull.EnemyHull != actor : hull.Hull != actor)) Error("Hull role, faction or level does not match the NPC.");
                if (hull.MaximumHullMin < 1 || hull.MaximumHullMax < hull.MaximumHullMin ||
                    hull.CargoCapacityMin < 0 || hull.CargoCapacityMax < hull.CargoCapacityMin ||
                    hull.AccelerationMin < 0 || hull.AccelerationMax < hull.AccelerationMin ||
                    hull.TurnRadius < 0 || hull.VisualSize <= 0) Error("Hull parameter ranges are invalid.");
                for (int i = 0; i < 7; i++)
                    if ((hull.AvailableComponentSlots & (ShipComponentSlots)(1 << i)) != 0 && !available.Contains((ShipComponentSlot)i))
                        Error($"No equipment variants for hull slot {(ShipComponentSlot)i}.");
                if (level == 0 && (hull.AvailableComponentSlots != ShipComponentSlots.Engine || hull.WeaponSlotCount != 0 || hull.MarketAvailable))
                    Error("The starter hull must have only an engine and must be excluded from the market.");
            }
        return issues;
    }
}
