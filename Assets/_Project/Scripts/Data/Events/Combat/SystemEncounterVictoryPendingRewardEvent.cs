/// <summary>
/// Победа в системном столкновении с ещё не полученной наградой.
/// </summary>
public readonly struct SystemEncounterVictoryPendingRewardEvent
    {
        /// <summary>Идентификатор боевого столкновения.</summary>
        public readonly string EncounterId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Число уничтоженных игроком целей.</summary>
        public readonly int PlayerKills;

        public SystemEncounterVictoryPendingRewardEvent(
            string encounterId,
            string systemId,
            int playerKills)
        {
            EncounterId = encounterId;
            SystemId = systemId;
            PlayerKills = playerKills;
        }
    }