using System;

[Serializable]
public class RuntimeCargoEntryData //runtime-модель одной записи инвентаря
{
    public string ItemId;
    public int Quantity;
    // Non-empty for a physical mission-owned stack. Ordinary trade cannot remove it.
    public string MissionRuntimeId;

    public RuntimeCargoEntryData(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}