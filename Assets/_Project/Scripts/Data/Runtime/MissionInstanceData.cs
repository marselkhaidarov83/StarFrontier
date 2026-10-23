using System;

[Serializable]
public class MissionInstanceData
{
        public string MissionRuntimeId;
        public string TemplateId;

        public string Title;
        public string Description;

        public MissionStatus Status;
        public MissionType MissionType;

        public string SourcePlanetId;
        public string SourceSystemId;

        public string TargetPlanetId;
        public string TargetSystemId;

        public MissionRewardData Reward;
        public MissionObjectiveData Objective;
        public string PirateGroupSpawnRuleId;
        public string PirateGroupNpcId;

        public bool IsReadyToTurnIn;

        // Zero means no deadline, including records from older saves.
        public int DurationDays;
        public int AcceptedAtDay;
        public int ExpiresAtDay;
        public int FinishedAtDay;
        public string LastTransitionReason;
}