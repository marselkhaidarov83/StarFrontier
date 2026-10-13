using UnityEngine;

/// <summary>
/// Старт боевого runtime-эпизода с идентификаторами столкновения и системы, числом врагов и союзников.
/// </summary>
public struct CombatRuntimeStartedEvent2A
{
    /// <summary>Идентификатор боевого столкновения.</summary>
    public string EncounterId { get; }
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }
    /// <summary>Количество врагов в столкновении.</summary>
    public int EnemyCount { get; }
    /// <summary>Количество союзников в столкновении.</summary>
    public int AllyCount { get; }

    public CombatRuntimeStartedEvent2A(
        string encounterId,
        string systemId,
        int enemyCount,
        int allyCount)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        EnemyCount = enemyCount;
        AllyCount = allyCount;
    }
}
