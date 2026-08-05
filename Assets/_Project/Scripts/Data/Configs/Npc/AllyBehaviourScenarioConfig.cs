using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AllyBehaviourScenarioConfig",
    menuName = "StarFrontier/Configs/Npc/Ally Behaviour Scenario")]
public sealed class AllyBehaviourScenarioConfig : BaseConfig
{
    [Header("Behavior Weights")]
    [SerializeField]
    private SystemNpcBehaviorWeight[] behaviorWeights =
        new SystemNpcBehaviorWeight[0];

    [Header("Combat")]
    [SerializeField]
    [Range(0f, 100f)]
    private float engageEnemiesWeight = 100f;

    public IReadOnlyList<SystemNpcBehaviorWeight> BehaviorWeights =>
        behaviorWeights;

    public float EngageEnemiesWeight =>
        engageEnemiesWeight;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (behaviorWeights == null)
            behaviorWeights = new SystemNpcBehaviorWeight[0];

        engageEnemiesWeight =
            Mathf.Clamp(engageEnemiesWeight, 0f, 100f);
    }
#endif
}