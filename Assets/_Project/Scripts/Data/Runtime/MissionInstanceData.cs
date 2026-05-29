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
        public PirateGroupSpawnRuleConfig PirateGroupSpawnRuleConfig;
        public string PirateGroupNpcId;

        public bool IsReadyToTurnIn;
}