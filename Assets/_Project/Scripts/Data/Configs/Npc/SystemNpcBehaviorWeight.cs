using System;
using UnityEngine;

[Serializable]
public sealed class SystemNpcBehaviorWeight
{
    [SerializeField]
    private SystemNpcBehaviorType behaviorType;

    [SerializeField]
    private NpcBehaviourTargetStationType targetStationType =
        NpcBehaviourTargetStationType.None;

    [SerializeField]
    private NpcBehaviourTargetUnitSide targetUnitSide =
        NpcBehaviourTargetUnitSide.None;

    [SerializeField]
    private NpcBehaviourTargetUnitType targetUnitType =
        NpcBehaviourTargetUnitType.None;

    [SerializeField]
    [Range(0, 100)]
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
