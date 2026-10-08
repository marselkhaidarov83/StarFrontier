using UnityEngine;

// Конфиг RewardConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[System.Serializable]
public class RewardItemChance
{
    [Tooltip("Параметр Item. Используется связанными игровыми системами этого конфига.")]
    public ItemConfig Item;
    [Tooltip("Параметр Chance. Используется связанными игровыми системами этого конфига.")]
    public float Chance;
    [Tooltip("Количество для параметра MinAmount. Используется соответствующей системой при генерации или расчёте.")]
    public int MinAmount;
    [Tooltip("Количество для параметра MaxAmount. Используется соответствующей системой при генерации или расчёте.")]
    public int MaxAmount;
}

[CreateAssetMenu(fileName = "RewardConfig", menuName = "StarFrontier/Configs/Combat/Reward")]
public class RewardConfig : BaseConfig
{
    [Header("Credits")]
    [Tooltip("Минимальное значение параметра creditsMin. Используется как нижняя граница диапазона.")]
    [SerializeField] private int creditsMin;
    [Tooltip("Максимальное значение параметра creditsMax. Используется как верхняя граница диапазона.")]
    [SerializeField] private int creditsMax;

    [Header("Item Drops")]
    [Tooltip("Параметр itemDrops. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private RewardItemChance[] itemDrops;

    public int CreditsMin => creditsMin;
    public int CreditsMax => creditsMax;
    public RewardItemChance[] ItemDrops => itemDrops;
}
