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
            total += Math.Max(0, entries[i].Quantity);
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

        RuntimeCargoEntryData entry = entries.FirstOrDefault(x => x.ItemId == itemId);
        return entry == null ? 0 : Math.Max(0, entry.Quantity);
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

        RuntimeCargoEntryData entry = entries.FirstOrDefault(x => x.ItemId == itemId);

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

        return GetQuantity(itemId) >= quantityToRemove;
    }

    public bool RemoveItem(string itemId, int quantityToRemove)
    {
        if (!CanRemoveItem(itemId, quantityToRemove))
        {
            return false;
        }

        RuntimeCargoEntryData entry = entries.FirstOrDefault(x => x.ItemId == itemId);

        if (entry == null)
        {
            return false;
        }

        entry.Quantity -= quantityToRemove;

        CleanupInvalidEntries();
        return true;
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