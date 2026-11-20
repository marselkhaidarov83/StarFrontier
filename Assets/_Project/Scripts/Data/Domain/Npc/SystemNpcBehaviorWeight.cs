using System;
using UnityEngine;

// Встроенная запись профиля поведения: действие и относительный вес его выбора.
[Serializable]
public sealed class SystemNpcBehaviorWeight
{
    [SerializeField, Tooltip("Действие неигрового корабля, участвующее в выборе поведения.")]
    private SystemNpcBehaviorType behaviorType;

    [SerializeField, Range(0, 1000), Tooltip("Относительный вес выбора действия.")]
    private int weight = 10;

    public SystemNpcBehaviorType BehaviorType => behaviorType;
    public int Weight => weight;

    public SystemNpcBehaviorWeight WithType(SystemNpcBehaviorType type)
    { var copy = (SystemNpcBehaviorWeight)MemberwiseClone(); copy.behaviorType = type; return copy; }
}
