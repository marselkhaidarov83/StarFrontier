using UnityEngine;

/// <summary>
/// Поражение игрока в боевом столкновении; содержит причину поражения.
/// </summary>
public struct CombatDefeatEvent2A
{
    /// <summary>Идентификатор боевого столкновения.</summary>
    public string EncounterId { get; }
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }
    /// <summary>Причина завершения или поражения.</summary>
    public SystemEncounterDefeatReason Reason { get; }

    public CombatDefeatEvent2A(
        string encounterId,
        string systemId,
        SystemEncounterDefeatReason reason)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        Reason = reason;
    }
}
