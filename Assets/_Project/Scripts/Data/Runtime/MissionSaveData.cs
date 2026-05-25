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

        public string SourceSystemId;
        public string TargetSystemId;

        public MissionRewardData Reward;
        public MissionObjectiveData Objective;

        public bool IsReadyToTurnIn;
}