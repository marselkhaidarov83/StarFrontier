using UnityEngine;

/// <summary>
/// Победа в боевом столкновении; сообщает идентификатор боя и число убийств игрока.
/// </summary>
public struct CombatVictoryEvent2A
{
    /// <summary>Идентификатор боевого столкновения.</summary>
    public string EncounterId { get; }
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }
    /// <summary>Число уничтоженных игроком целей.</summary>
    public int PlayerKills { get; }

    public CombatVictoryEvent2A(
        string encounterId,
        string systemId,
        int playerKills)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        PlayerKills = playerKills;
    }
}
