using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EquipmentSlotGroup
{
    public ShipComponentSlot Slot;
    public List<ShipEquipmentConfig> Equipment = new();
}

[CreateAssetMenu(fileName = "equipment_group_", menuName = "StarFrontier/Configs/Ship/Equipment Group")]
public sealed class EquipmentGroupConfig : BaseConfig
{
    [Min(0)] public int Level = 1;
    [SerializeField] private List<EquipmentSlotGroup> slotGroups = new();
    public IReadOnlyList<EquipmentSlotGroup> SlotGroups => slotGroups;

    public List<ShipEquipmentConfig> PickComponents(ShipComponentSlots allowed)
    {
        var result = new List<ShipEquipmentConfig>();
        var occupied = new HashSet<ShipComponentSlot>();
        if (slotGroups == null) return result;
        foreach (EquipmentSlotGroup group in slotGroups)
        {
            if (group == null || (allowed & (ShipComponentSlots)(1 << (int)group.Slot)) == 0 || occupied.Contains(group.Slot)) continue;
            List<ShipEquipmentConfig> candidates = Candidates(group);
            if (candidates.Count == 0) continue;
            result.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            occupied.Add(group.Slot);
        }
        return result;
    }

    public ShipEquipmentConfig PreviewComponent(ShipComponentSlot slot)
    {
        if (slotGroups == null) return null;
        foreach (EquipmentSlotGroup group in slotGroups)
        {
            if (group == null || group.Slot != slot) continue;
            List<ShipEquipmentConfig> candidates = Candidates(group);
            if (candidates.Count > 0) return candidates[0];
        }
        return null;
    }

    public List<ShipEquipmentConfig> PreviewComponents(ShipComponentSlots allowed)
    {
        var result = new List<ShipEquipmentConfig>();
        for (int i = 0; i < 7; i++)
        {
            var slot = (ShipComponentSlot)i;
            ShipEquipmentConfig component = PreviewComponent(slot);
            if ((allowed & (ShipComponentSlots)(1 << i)) != 0 && component != null) result.Add(component);
        }
        return result;
    }

    private List<ShipEquipmentConfig> Candidates(EquipmentSlotGroup group)
    {
        var result = new List<ShipEquipmentConfig>();
        var seen = new HashSet<string>();
        if (group.Equipment != null)
            foreach (ShipEquipmentConfig item in group.Equipment)
                if (item != null && item.Slot == group.Slot && item.Level == Math.Max(1, Level) &&
                    !string.IsNullOrWhiteSpace(item.Id) && seen.Add(item.Id)) result.Add(item);
        return result;
    }
}
