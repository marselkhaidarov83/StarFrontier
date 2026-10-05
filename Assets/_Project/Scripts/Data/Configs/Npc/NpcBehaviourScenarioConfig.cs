using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "NpcBehaviourScenarioConfig",
    menuName = "StarFrontier/Configs/Npc/NPC Behaviour Scenario")]
public sealed class NpcBehaviourScenarioConfig : BaseConfig
{
    [Header("Behavior Weights")]
    [FormerlySerializedAs("behaviorWeights")]
    [SerializeField]
    private SystemNpcBehaviorWeight[] behaviorWeights =
        new SystemNpcBehaviorWeight[0];

    [Header("Combat")]
    [SerializeField]
    [Range(0f, 1000f)]
    private float engageEnemiesWeight = 1000f;

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
            Mathf.Clamp(engageEnemiesWeight, 0f, 1000f);
    }
#endif
}
