using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Конфиг NpcBehaviourScenarioConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "NpcBehaviourScenarioConfig",
    menuName = "StarFrontier/Configs/Npc/NPC Behaviour Scenario")]
public sealed class NpcBehaviourScenarioConfig : BaseConfig
{
    [Header("Behavior Weights")]
    [FormerlySerializedAs("behaviorWeights")]
    [SerializeField]
    [Tooltip("Веса поведения для кораблей этой группы.")]
    private SystemNpcBehaviorWeight[] behaviorWeights =
        new SystemNpcBehaviorWeight[0];

    [Header("Combat")]
    [SerializeField]
    [Range(0f, 1000f)]
    [Tooltip("Вес выбора этой записи относительно других вариантов. Используется генератором или логикой выбора.")]
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
