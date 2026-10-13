using System;
using UnityEngine;

// Конфиг PirateGroupEntryConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[Serializable]
public sealed class PirateGroupEntryConfig
{
    [Header("Pirate")]
    [Tooltip("Конфиг пирата, который входит в эту запись группы.")]
    [SerializeField] private PirateConfig pirateConfig;

    [Header("Count")]
    [Tooltip("Минимальное количество таких кораблей в группе.")]
    [SerializeField] private int minCount = 1;
    [Tooltip("Максимальное количество таких кораблей в группе.")]
    [SerializeField] private int maxCount = 1;

    public PirateConfig PirateConfig => pirateConfig;
    public int MinCount => minCount;
    public int MaxCount => maxCount;
}
