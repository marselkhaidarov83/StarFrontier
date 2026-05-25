using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "PirateGroupSpawnRuleConfig", menuName = "StarFrontier/Configs/Pirate group spawn rule")]
public sealed class PirateGroupSpawnRuleConfig : BaseConfig
{
    [Header("Group Composition")]
    [SerializeField] private List<PirateGroupEntryConfig> pirates = new();

    [Header("Position")]
    [SerializeField] private Vector3 startPosition = new Vector3(0, 0, -2);

    [Header("Behavior")]
    [SerializeField] private List<SystemNpcBehaviorWeight> behaviorWeights = new();

    public IReadOnlyList<PirateGroupEntryConfig> Pirates => pirates;
    public Vector3 StartPosition => startPosition;
    public List<SystemNpcBehaviorWeight> BehaviorWeights => behaviorWeights;
}