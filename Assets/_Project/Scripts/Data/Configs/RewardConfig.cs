using UnityEngine;

[System.Serializable]
public class RewardItemChance
{
    public ItemConfig Item;
    public float Chance;
    public int MinAmount;
    public int MaxAmount;
}

[CreateAssetMenu(fileName = "RewardConfig", menuName = "StarFrontier/Configs/Reward")]
public class RewardConfig : BaseConfig
{
    [Header("Credits")]
    [SerializeField] private int creditsMin;
    [SerializeField] private int creditsMax;

    [Header("Item Drops")]
    [SerializeField] private RewardItemChance[] itemDrops;

    public int CreditsMin => creditsMin;
    public int CreditsMax => creditsMax;
    public RewardItemChance[] ItemDrops => itemDrops;
}