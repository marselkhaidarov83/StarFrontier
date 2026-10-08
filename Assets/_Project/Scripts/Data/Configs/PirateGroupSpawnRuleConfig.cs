using System;
using System.Collections.Generic;
using UnityEngine;

// Конфиг PirateGroupSpawnRuleConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[Serializable]
[CreateAssetMenu(fileName = "PirateGroupSpawnRuleConfig", menuName = "StarFrontier/Configs/Npc/Pirate group spawn rule")]
public sealed class PirateGroupSpawnRuleConfig : BaseConfig
{
    [Header("Group Composition")]
    [Tooltip("Список пиратов, которые могут появиться в группе.")]
    [SerializeField] private List<PirateGroupEntryConfig> pirates = new();

    [Header("Position")]
    [Tooltip("Стартовая позиция создаваемой группы.")]
    [SerializeField] private Vector3 startPosition = new Vector3(0, 0, -2);

    [Header("Behavior")]
    [Tooltip("Веса поведения для кораблей этой группы.")]
    [SerializeField] private List<SystemNpcBehaviorWeight> behaviorWeights = new();

    public IReadOnlyList<PirateGroupEntryConfig> Pirates => pirates;
    public Vector3 StartPosition => startPosition;
    public List<SystemNpcBehaviorWeight> BehaviorWeights => behaviorWeights;
}
