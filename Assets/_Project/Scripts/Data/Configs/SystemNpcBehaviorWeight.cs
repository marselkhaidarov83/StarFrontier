using System;
using UnityEngine;

    [Serializable]
    public sealed class SystemNpcBehaviorWeight
    {
        [SerializeField] private SystemNpcBehaviorType behaviorType;
        [SerializeField] [Range(0, 100)] private int weight = 10;

        public SystemNpcBehaviorType BehaviorType => behaviorType;
        public int Weight => weight;
    }