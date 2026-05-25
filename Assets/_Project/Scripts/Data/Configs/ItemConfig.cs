using UnityEngine;

[CreateAssetMenu(fileName = "ItemConfig", menuName = "StarFrontier/Configs/Item")]
public class ItemConfig : BaseConfig
{
    [Header("Market Data")]
    [SerializeField] private ItemCategory itemCategory;
    [SerializeField] private int basePrice;
    [SerializeField] private int cargoSize;
    [SerializeField] private ItemRarity rarity;
    [SerializeField] private bool isLegal = true;

    public ItemCategory ItemCategory => itemCategory;
    public int BasePrice => basePrice;
    public int CargoSize => cargoSize;
    public ItemRarity Rarity => rarity;
    public bool IsLegal => isLegal;
}