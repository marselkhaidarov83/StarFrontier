using System;

    [Serializable]
    public sealed class ActiveSystemEncounter
    {
        public string EncounterId;
        public string SystemId;

        public SystemEncounterState State;

        public int EnemiesAlive;
        public int AlliesAlive;

        public int PlayerKills;
        public bool PlayerDestroyed;

        public bool PlayerIsOnPlanet;
        public int DaysPlayerStayedOnPlanetDuringEncounter;

        public SystemEncounterDefeatReason DefeatReason;

        public bool HasAliveEnemies => EnemiesAlive > 0;
        public bool HasAliveAllies => AlliesAlive > 0;
        public bool PlayerParticipated => PlayerKills > 0;

        public bool IsActive => State == SystemEncounterState.Active;
        public bool IsDefeated => State == SystemEncounterState.Defeated;
        public bool IsVictoryPendingReward => State == SystemEncounterState.VictoryPendingReward;
        public bool IsResolved => State == SystemEncounterState.Resolved;

        public bool CanClaimGovernmentReward =>
            State == SystemEncounterState.VictoryPendingReward;
    }