using System;
using UnityEngine;

[Serializable]
public sealed class AllyBehaviourScenarioEntry
{
    [Header("Scenario")]
    [SerializeField]
    private AllyBehaviourScenario scenario =
        AllyBehaviourScenario.Normal;

    [Header("Behavior Profile")]
    [SerializeField]
    private AllyBehaviourScenarioConfig behaviorConfig;

    public AllyBehaviourScenario Scenario =>
        scenario;

    public AllyBehaviourScenarioConfig BehaviorConfig =>
        behaviorConfig;

    public bool IsValid()
    {
        return behaviorConfig != null;
    }
}