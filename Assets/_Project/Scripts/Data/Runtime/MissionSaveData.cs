using System;

[Serializable]
public class MissionSaveData
{
        public string MissionRuntimeId;
        public string TemplateId;

        public string Title;
        public string Description;

        public MissionType MissionType;
        public MissionStatus Status;

        public string SourcePlanetId;
        public string TargetPlanetId;
        public string PirateGroupSpawnRuleId;
        public string PirateGroupNpcId;
        public string SourceSystemId;
        public string TargetSystemId;

        public MissionRewardData Reward;
        public MissionObjectiveData Objective;

        public bool IsReadyToTurnIn;

        // Zero means no deadline, including records from older saves.
        public int DurationDays;
        public int AcceptedAtDay;
        public int ExpiresAtDay;
        public int FinishedAtDay;
        public string LastTransitionReason;
}