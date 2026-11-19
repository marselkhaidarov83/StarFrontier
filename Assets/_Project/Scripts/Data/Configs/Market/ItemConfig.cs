using UnityEngine;

[CreateAssetMenu(fileName = "ItemConfig", menuName = "StarFrontier/Configs/Market/Item")]
public class ItemConfig : SpaceLootCombatConfig
{
    [Header("Market Data")]
    [SerializeField] private ItemCategory itemCategory;
    [SerializeField] private int basePrice;
    [SerializeField, Range(1, 10), Tooltip("Минимальный уровень развития рынка для открытия товара.")]
    private int marketDevelopmentLevel = 1;
    [SerializeField, Tooltip("Занимает места в трюме за одну единицу.")] private int cargoSize;
    [SerializeField] private bool isLegal = true;

    public ItemCategory ItemCategory => itemCategory;
    public int BasePrice => basePrice;
    public int MarketDevelopmentLevel => marketDevelopmentLevel;
    public int CargoSize => cargoSize;
    public bool IsLegal => isLegal;
}
