using System;
using UnityEngine;

// Конфиг NpcBehaviourTransitionMatrixConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "NpcBehaviourTransitionMatrixConfig",
    menuName = "StarFrontier/Configs/Npc/NPC Behaviour Transition Matrix")]
public sealed class NpcBehaviourTransitionMatrixConfig : BaseConfig
{
    [SerializeField]
    [Tooltip("Параметр rules. Используется связанными игровыми системами этого конфига.")]
    private NpcBehaviourTransitionRule[] rules =
        Array.Empty<NpcBehaviourTransitionRule>();

    public bool IsNextBehaviorAllowed(
        SystemNpcBehaviorType previousBehavior,
        SystemNpcBehaviorType nextBehavior)
    {
        if (previousBehavior == SystemNpcBehaviorType.None)
            return true;

        if (rules == null)
            return false;

        for (int i = 0; i < rules.Length; i++)
        {
            NpcBehaviourTransitionRule rule = rules[i];

            if (rule == null ||
                rule.PreviousBehavior != previousBehavior)
            {
                continue;
            }

            return rule.Contains(nextBehavior);
        }

        return false;
    }
}

[Serializable]
public sealed class NpcBehaviourTransitionRule
{
    [SerializeField]
    [Tooltip("Параметр previousBehavior. Используется связанными игровыми системами этого конфига.")]
    private SystemNpcBehaviorType previousBehavior;

    [SerializeField]
    [Tooltip("Переключатель allowedNextBehaviors. Включает или выключает соответствующее правило или отображение.")]
    private SystemNpcBehaviorType[] allowedNextBehaviors =
        Array.Empty<SystemNpcBehaviorType>();

    public SystemNpcBehaviorType PreviousBehavior =>
        previousBehavior;

    public bool Contains(SystemNpcBehaviorType behavior)
    {
        if (allowedNextBehaviors == null)
            return false;

        for (int i = 0; i < allowedNextBehaviors.Length; i++)
        {
            if (allowedNextBehaviors[i] == behavior)
                return true;
        }

        return false;
    }
}
