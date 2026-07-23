using System;

[Serializable]
public sealed class MarketRuntimeItemState
{
    public string ItemId;
    public int Quantity;
    public float BuyMultiplier = 1f;
    public float SellMultiplier = 0.5f;
    public bool IsAvailable = true;
}