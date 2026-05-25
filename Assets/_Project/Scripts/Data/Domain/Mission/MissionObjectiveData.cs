using System;

[Serializable]
    public class MissionObjectiveData
    {
        public string TargetSystemId;
        public string TargetEnemyId;

        public int RequiredAmount;
        public int CurrentAmount;

        public string MissionCargoItemId;
        public int MissionCargoAmount;

        public bool IsMissionCargoGranted;
        public bool IsMissionCargoDelivered;
    }