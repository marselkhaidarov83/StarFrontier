using System;
using UnityEngine;
using UnityEngine.Serialization;

// Встроенная запись EnemyConfig: сценарий и ссылка на профиль поведения.
[Serializable]
public sealed class NpcBehaviourScenarioEntry
{
    [Header("Scenario")]
    [SerializeField]
    [Tooltip("Сценарий поведения, при котором используется эта запись.")]
    private AllyBehaviourScenario scenario;

    [Header("Behavior Profile")]
    [FormerlySerializedAs("behaviorConfig")]
    [SerializeField]
    [Tooltip("Профиль поведения, который применяется для выбранного сценария.")]
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
