using UnityEngine;

[System.Serializable]
public sealed class EnemyGroupEntryConfig
{
    [SerializeField] private EnemyConfig enemyConfig;

    [SerializeField] [Min(1)] private int minCount = 1;
    [SerializeField] [Min(1)] private int maxCount = 1;

    public EnemyConfig EnemyConfig => enemyConfig;

    public int MinCount => minCount;

    public int MaxCount => maxCount;

    public bool IsValid()
    {
        if (enemyConfig == null)
            return false;

        if (minCount <= 0)
            return false;

        if (maxCount < minCount)
            return false;

        return true;
    }

#if UNITY_EDITOR
    public void Validate()
    {
        minCount =
            Mathf.Max(1, minCount);

        maxCount =
            Mathf.Max(minCount, maxCount);
    }
#endif
}