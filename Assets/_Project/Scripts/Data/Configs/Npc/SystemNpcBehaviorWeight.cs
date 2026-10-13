using System;
using UnityEngine;

// Конфиг SystemNpcBehaviorWeight содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[Serializable]
public sealed class SystemNpcBehaviorWeight
{
    [SerializeField]
    [Tooltip("Тип поведения неигрового корабля. Используется при выборе действия в симуляции.")]
    private SystemNpcBehaviorType behaviorType;

    [SerializeField]
    [Tooltip("Ограничение по типу станции-цели для поведения.")]
    private NpcBehaviourTargetStationType targetStationType =
        NpcBehaviourTargetStationType.None;

    [SerializeField]
    [Tooltip("Ограничение по стороне цели для поведения.")]
    private NpcBehaviourTargetUnitSide targetUnitSide =
        NpcBehaviourTargetUnitSide.None;

    [SerializeField]
    [Tooltip("Ограничение по типу цели для поведения.")]
    private NpcBehaviourTargetUnitType targetUnitType =
        NpcBehaviourTargetUnitType.None;

    [SerializeField]
    [Range(0, 1000)]
    [Tooltip("Относительный вес выбора этой записи. Чем больше значение, тем чаще запись выбирается среди других подходящих.")]
    private int weight = 10;

    public SystemNpcBehaviorType BehaviorType =>
        behaviorType;

    public NpcBehaviourTargetStationType TargetStationType =>
        targetStationType;

    public NpcBehaviourTargetUnitSide TargetUnitSide =>
        targetUnitSide;

    public NpcBehaviourTargetUnitType TargetUnitType =>
        targetUnitType;

    public int Weight =>
        weight;

    public bool HasStationTarget =>
        targetStationType != NpcBehaviourTargetStationType.None;

    public bool HasUnitTarget =>
        targetUnitSide != NpcBehaviourTargetUnitSide.None &&
        targetUnitType != NpcBehaviourTargetUnitType.None;
}
