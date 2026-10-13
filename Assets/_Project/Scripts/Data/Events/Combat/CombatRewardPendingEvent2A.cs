using UnityEngine;

/// <summary>
/// Победа ожидает выдачи боевой награды, но награда ещё не подтверждена как полученная.
/// </summary>
public struct CombatRewardPendingEvent2A
{
    /// <summary>Идентификатор боевого столкновения.</summary>
    public string EncounterId { get; }
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }
    /// <summary>Число уничтоженных игроком целей.</summary>
    public int PlayerKills { get; }

    public CombatRewardPendingEvent2A(
        string encounterId,
        string systemId,
        int playerKills)
    {
        EncounterId = encounterId;
        SystemId = systemId;
        PlayerKills = playerKills;
    }
}
