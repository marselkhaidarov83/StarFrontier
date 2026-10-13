using UnityEngine;

// Конфиг EnemyGroupEntryConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[System.Serializable]
public sealed class EnemyGroupEntryConfig
{
    [Tooltip("Конфиг врага, который входит в эту запись группы.")]
    [SerializeField] private EnemyConfig enemyConfig;

    [Tooltip("Минимальное количество таких кораблей в группе.")]
    [SerializeField] [Min(0)] private int minCount = 0;
    [Tooltip("Максимальное количество таких кораблей в группе.")]
    [SerializeField] [Min(1)] private int maxCount = 1;

    [Tooltip("Относительный вес выбора этой записи. Чем больше значение, тем чаще запись выбирается среди других подходящих.")]
    [SerializeField] [Min(1)] private int weight = 1;

    public EnemyConfig EnemyConfig => enemyConfig;

    public int MinCount => minCount;

    public int MaxCount => maxCount;

    public int Weight => weight;

    public bool IsValid()
    {
        if (enemyConfig == null)
            return false;

        if (minCount < 0)
            return false;

        if (maxCount <= 0)
            return false;

        if (maxCount < minCount)
            return false;

        if (weight <= 0)
            return false;

        return true;
    }

#if UNITY_EDITOR
    public void Validate()
    {
        minCount =
            Mathf.Max(0, minCount);

        maxCount =
            Mathf.Max(minCount, maxCount);

        weight =
            Mathf.Max(1, weight);
    }
#endif
}
