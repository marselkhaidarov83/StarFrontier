using System;

[Serializable]
public class MarketItemEntry
{
    public ItemConfig ItemConfig;
    public int Quantity;
    public float BuyMultiplier = 1f;
    public float SellMultiplier = 0.5f;
    public bool IsAvailable = true;
}