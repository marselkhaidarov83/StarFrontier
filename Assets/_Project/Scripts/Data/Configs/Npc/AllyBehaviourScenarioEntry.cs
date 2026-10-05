using System;
using UnityEngine;

[Serializable]
public sealed class AllyBehaviourScenarioEntry
{
    [Header("Scenario")]
    [SerializeField]
    private AllyBehaviourScenario scenario;

    [Header("Behavior Profile")]
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
