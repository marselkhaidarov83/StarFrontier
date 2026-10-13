using UnityEngine;

/// <summary>
/// Уничтожение боевой цели: вид цели и источник уничтожения для логики столкновения.
/// </summary>
public struct CombatTargetDestroyedEvent2A
{
    /// <summary>Идентификатор боевого столкновения.</summary>
    public string EncounterId { get; }
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }
    /// <summary>Тип цели, к которой относится событие.</summary>
    public string TargetType { get; }
    /// <summary>Идентификатор или обозначение уничтожившего объекта.</summary>
    public string DestroyedBy { get; }

    public CombatTargetDestroyedEvent2A(
        string encounterId,
        string systemId,
        string targetType,
        string destroyedBy)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        TargetType = targetType;
        DestroyedBy = destroyedBy;
    }
}
