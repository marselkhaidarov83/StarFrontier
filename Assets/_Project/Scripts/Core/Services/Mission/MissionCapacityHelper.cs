using UnityEngine;

public static class MissionCapacityHelper
    {
        public static int CountMissionsInWork(IMissionService missionService)
        {
            int result = 0;

            if (missionService == null)
                return result;

            foreach (MissionInstanceData mission in missionService.GetActiveMissions())
            {
                if (mission == null)
                    continue;

                if (MissionLifecycleRules.IsActive(mission.Status))
                    result++;
            }

            AppLog.Info(DebugLogChannel.Mission,
                typeof(MissionCapacityHelper),
                "CountMissionsInWork = " + result);
            return result;
        }

        public static bool CanAcceptMoreMissions(IMissionService missionService)
        {
            return CountMissionsInWork(missionService) < Bootstrapper.Instance.MaxAcceptedMissionCount;
        }
    }