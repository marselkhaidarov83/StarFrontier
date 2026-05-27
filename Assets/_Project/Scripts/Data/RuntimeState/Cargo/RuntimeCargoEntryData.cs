using System;

[Serializable]
public class RuntimeCargoEntryData //runtime-модель одной записи инвентаря
{
    public string ItemId;
    public int Quantity;

    public RuntimeCargoEntryData(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}