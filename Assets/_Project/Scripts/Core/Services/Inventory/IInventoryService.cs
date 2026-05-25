using System.Collections.Generic;

public interface IInventoryService
{
    IReadOnlyList<RuntimeCargoEntryData> GetItems();
    int GetItemCount(string itemId);
    int GetUsedCargo();
    int GetMaxCargo();
    int GetFreeCargo();
    bool CanAddItem(string itemId, int quantity);
    bool AddItem(string itemId, int quantity);
    bool RemoveItem(string itemId, int quantity);
    void Clear();
}