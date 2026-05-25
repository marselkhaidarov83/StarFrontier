using System.Collections.Generic;
using UnityEngine;

public static class SystemNpcBehaviorSelector
{
    public static SystemNpcBehaviorType PickBehavior(
        IReadOnlyList<SystemNpcBehaviorWeight> weights)
    {
        if (weights == null || weights.Count == 0)
            return SystemNpcBehaviorType.PatrolSystem;

        int totalWeight = 0;

        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] == null)
                continue;

            totalWeight += Mathf.Max(0, weights[i].Weight);
        }

        if (totalWeight <= 0)
            return SystemNpcBehaviorType.PatrolSystem;

        int roll = Random.Range(0, totalWeight);
        int cursor = 0;

        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] == null)
                continue;

            int weight = Mathf.Max(0, weights[i].Weight);

            if (weight <= 0)
                continue;

            cursor += weight;

            if (roll < cursor)
                return weights[i].BehaviorType;
        }

        return SystemNpcBehaviorType.PatrolSystem;
    }
}