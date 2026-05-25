using System;
using UnityEngine;

[Serializable]
public sealed class PirateGroupEntryConfig
{
    [Header("Pirate")]
    [SerializeField] private PirateConfig pirateConfig;

    [Header("Count")]
    [SerializeField] private int minCount = 1;
    [SerializeField] private int maxCount = 1;

    public PirateConfig PirateConfig => pirateConfig;
    public int MinCount => minCount;
    public int MaxCount => maxCount;
}