/// <summary>
/// Получение награды от правительства; сигнал подтверждённой выдачи.
/// </summary>
public readonly struct GovernmentRewardClaimedEvent
    {
        /// <summary>Идентификатор боевого столкновения.</summary>
        public readonly string EncounterId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Количество кредитов, передаваемое событием.</summary>
        public readonly int Credits;
        /// <summary>Количество очков опыта.</summary>
        public readonly int Xp;

        public GovernmentRewardClaimedEvent(
            string encounterId,
            string systemId,
            int credits,
            int xp)
        {
            EncounterId = encounterId;
            SystemId = systemId;
            Credits = credits;
            Xp = xp;
        }
    }