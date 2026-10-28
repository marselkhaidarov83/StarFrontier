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

        public string BattleEncounterId;
        public int BattleInvasionGeneration;
        public int BattleRewardDeflator;
        public bool BattleWon;
        public List<string> BattleKilledEnemyIds = new();

        public string RouteId;
        public bool RouteIsUpgrade;
        public int RouteExpectedLevel;
        public bool RouteDirectSurveyed;
        public bool RouteReturnedAfterDirectSurvey;
        public bool RouteIndirectSurveyed;
        public List<string> RouteSurveySystemIds = new();

        public bool IsMissionCargoGranted;
        public bool IsMissionCargoDelivered;
    }
