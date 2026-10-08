using System;
using UnityEngine;

// Конфиг AllyBehaviourScenarioEntry содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[Serializable]
public sealed class AllyBehaviourScenarioEntry
{
    [Header("Scenario")]
    [SerializeField]
    [Tooltip("Сценарий поведения, при котором используется эта запись.")]
    private AllyBehaviourScenario scenario;

    [Header("Behavior Profile")]
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
