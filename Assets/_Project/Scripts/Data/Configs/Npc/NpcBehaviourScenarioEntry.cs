using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class NpcBehaviourScenarioEntry
{
    [Header("Scenario")]
    [SerializeField]
    private AllyBehaviourScenario scenario;

    [Header("Behavior Profile")]
    [FormerlySerializedAs("behaviorConfig")]
    [SerializeField]
    private NpcBehaviourScenarioConfig behaviorConfig;

    public AllyBehaviourScenario Scenario =>
        scenario;

    public NpcBehaviourScenarioConfig BehaviorConfig =>
        behaviorConfig;

    public bool IsValid()
    {
        return behaviorConfig != null;
    }
}
