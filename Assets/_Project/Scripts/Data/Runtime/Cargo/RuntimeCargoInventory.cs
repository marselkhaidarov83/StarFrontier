using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class RuntimeCargoInventory //состояние всего инвентаря корабля
{
    public List<RuntimeCargoEntryData> entries = new List<RuntimeCargoEntryData>();

    public int GetUsedCapacity()
    {
        int total = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null) total += Math.Max(0, entries[i].Quantity);
        }

        return total;
    }

    public int GetFreeCapacity(int maxCapacity)
    {
        return Math.Max(0, maxCapacity - GetUsedCapacity());
    }

    public int GetQuantity(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, entries.Where(x => x != null && x.ItemId == itemId)
            .Sum(x => (long)Math.Max(0, x.Quantity)));
    }

    public int GetTradeableQuantity(string itemId)
    {
        if (entries == null || string.IsNullOrWhiteSpace(itemId)) return 0;
        return (int)Math.Min(int.MaxValue, entries.Where(x => x != null && x.ItemId == itemId && string.IsNullOrEmpty(x.MissionRuntimeId))
            .Sum(x => (long)Math.Max(0, x.Quantity)));
    }

    public bool CanAddItem(string itemId, int quantityToAdd, int maxCapacity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (quantityToAdd <= 0)
        {
            return false;
        }

        int freeCapacity = GetFreeCapacity(maxCapacity);
        return freeCapacity >= quantityToAdd;
    }

    public bool AddItem(string itemId, int quantityToAdd, int maxCapacity)
    {
        if (!CanAddItem(itemId, quantityToAdd, maxCapacity))
        {
            return false;
        }

        RuntimeCargoEntryData entry = entries.FirstOrDefault(x => x != null && x.ItemId == itemId && string.IsNullOrEmpty(x.MissionRuntimeId));

        if (entry == null)
        {
            entries.Add(new RuntimeCargoEntryData(itemId, quantityToAdd));
        }
        else
        {
            entry.Quantity += quantityToAdd;
        }

        CleanupInvalidEntries();
        return true;
    }

    // Loot may overload the hold. Market and mission operations retain AddItem's capacity gate.
    public bool AddLootItem(string itemId, int quantityToAdd)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantityToAdd <= 0)
            return false;

        RuntimeCargoEntryData entry = entries.FirstOrDefault(x => x != null &&
            x.ItemId == itemId && string.IsNullOrEmpty(x.MissionRuntimeId));
        if (entry == null)
            entries.Add(new RuntimeCargoEntryData(itemId, quantityToAdd));
        else if (entry.Quantity > int.MaxValue - quantityToAdd)
            return false;
        else
            entry.Quantity += quantityToAdd;
        CleanupInvalidEntries();
        return true;
    }

    public bool CanRemoveItem(string itemId, int quantityToRemove)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (quantityToRemove <= 0)
        {
            return false;
        }

        return GetTradeableQuantity(itemId) >= quantityToRemove;
    }

    public bool RemoveItem(string itemId, int quantityToRemove)
    {
        if (!CanRemoveItem(itemId, quantityToRemove))
        {
            return false;
        }

        int remaining = quantityToRemove;
        foreach (var entry in entries)
        {
            if (entry == null || entry.ItemId != itemId || !string.IsNullOrEmpty(entry.MissionRuntimeId)) continue;
            int take = Math.Min(remaining, Math.Max(0, entry.Quantity));
            entry.Quantity -= take;
            remaining -= take;
            if (remaining == 0) break;
        }
        CleanupInvalidEntries();
        return remaining == 0;
    }

    // Dropping cargo may discard mission-owned items too; this is an explicit player action.
    public bool RemoveAnyItem(string itemId, int quantityToRemove)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantityToRemove <= 0 ||
            GetQuantity(itemId) < quantityToRemove)
            return false;
        int remaining = quantityToRemove;
        foreach (RuntimeCargoEntryData entry in entries)
        {
            if (entry == null || entry.ItemId != itemId) continue;
            int take = Math.Min(remaining, Math.Max(0, entry.Quantity));
            entry.Quantity -= take;
            remaining -= take;
            if (remaining == 0) break;
        }
        CleanupInvalidEntries();
        return remaining == 0;
    }

    public void Clear()
    {
        entries.Clear();
    }

    private void CleanupInvalidEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            RuntimeCargoEntryData entry = entries[i];

            if (entry == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.ItemId))
            {
                entries.RemoveAt(i);
                continue;
            }

            if (entry.Quantity <= 0)
            {
                entries.RemoveAt(i);
            }
        }
    }
}
