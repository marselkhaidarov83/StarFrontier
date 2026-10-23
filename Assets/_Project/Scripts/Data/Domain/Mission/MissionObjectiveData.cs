using System;
using System.Collections.Generic;

[Serializable]
    public class MissionObjectiveData
    {
        public string TargetSystemId;
        public string TargetEnemyId;

        public int RequiredAmount;
        public int CurrentAmount;

        public string MissionCargoItemId;
        public int MissionCargoAmount;
        public int MissionCargoVolume;
        public int DeliveryFuelCost;
        public int ReconEnemyJumps;
        public int ReconEnemyCountAtIssue;
        public float ReconScanDistance;
        public List<string> ReconObservedEnemyIds = new();
        public bool ReconIsInsideTarget;
        public string ReconLastAlliedSystemId;

        public bool IsMissionCargoGranted;
        public bool IsMissionCargoDelivered;
    }