using System;

[Serializable]
public class RewardItemEntry
{
    public string ItemId;
    public int Amount;

    public RewardItemEntry(string itemId, int amount)
    {
        ItemId = itemId;
        Amount = amount;
    }
}