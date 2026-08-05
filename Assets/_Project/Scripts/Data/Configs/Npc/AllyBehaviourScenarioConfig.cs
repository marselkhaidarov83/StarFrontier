using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AllyBehaviourScenarioConfig : BaseConfig
{
    [Header("Scenario")]
    [Tooltip(
        "Расширяемый строковый идентификатор сценария. " +
        "Примеры: normal, enemy_invasion, enemy_system_invasion.")]
    [SerializeField] private string scenarioId = "normal";

    [Header("Behavior Weights")]
    [SerializeField] private SystemNpcBehaviorWeight[] behaviorWeights =
        new SystemNpcBehaviorWeight[0];

    [Header("Combat")]
    [SerializeField]
    [Range(0f, 100f)]
    private float engageEnemiesWeight = 100f;

    public string ScenarioId => scenarioId;

    public IReadOnlyList<SystemNpcBehaviorWeight> BehaviorWeights =>
        behaviorWeights;

    public float EngageEnemiesWeight =>
        engageEnemiesWeight;

    public bool Matches(string requestedScenarioId)
    {
        if (string.IsNullOrWhiteSpace(requestedScenarioId))
            return false;

        if (string.IsNullOrWhiteSpace(scenarioId))
            return false;

        return string.Equals(
            scenarioId.Trim(),
            requestedScenarioId.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(scenarioId);
    }

#if UNITY_EDITOR
    public void Validate()
    {
        if (scenarioId == null)
            scenarioId = string.Empty;
        else
            scenarioId = scenarioId.Trim().ToLowerInvariant();

        if (behaviorWeights == null)
            behaviorWeights = new SystemNpcBehaviorWeight[0];

        engageEnemiesWeight =
            Mathf.Clamp(engageEnemiesWeight, 0f, 100f);
    }
#endif
}