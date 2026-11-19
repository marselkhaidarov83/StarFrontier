using System;

[Serializable]
public class MarketItemEntry
{
    public ItemConfig ItemConfig;
    [UnityEngine.Min(0)] public int Quantity;
    [UnityEngine.Tooltip("Изменение запаса за интервал: плюс — производство, минус — потребление.")]
    public int StockChange;
    [UnityEngine.Min(1)] public int MaximumStock = 100;
    [UnityEngine.Tooltip("0 — базовая цена ItemConfig (совместимость старых профилей).")]
    [UnityEngine.Min(0)] public int BasePrice;
    [UnityEngine.Min(0)] public int MaximumPrice;

}