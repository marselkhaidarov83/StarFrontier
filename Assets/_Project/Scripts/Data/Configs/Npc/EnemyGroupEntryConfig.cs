using UnityEngine;

[System.Serializable]
public sealed class EnemyGroupEntryConfig
{
    [SerializeField] private EnemyConfig enemyConfig;

    [SerializeField] [Min(0)] private int minCount = 0;
    [SerializeField] [Min(1)] private int maxCount = 1;

    [Tooltip("Relative pick weight for this enemy entry inside a generated group rule.")]
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