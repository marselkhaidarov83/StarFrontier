using System.Collections.Generic;

    public static class MissionSaveMapper
    {
        public static MissionSaveData ToSaveData(MissionInstanceData mission)
        {
            if (mission == null)
            {
                return null;
            }

            return new MissionSaveData
            {
                MissionRuntimeId = mission.MissionRuntimeId,
                TemplateId = mission.TemplateId,
                Title = mission.Title,
                Description = mission.Description,
                MissionType = mission.MissionType,
                Status = mission.Status,
                SourceSystemId = mission.SourceSystemId,
                TargetSystemId = mission.TargetSystemId,
                Reward = mission.Reward,
                Objective = mission.Objective,
                IsReadyToTurnIn = mission.IsReadyToTurnIn
            };
        }

        public static MissionInstanceData ToRuntimeData(MissionSaveData saveData)
        {
            if (saveData == null)
            {
                return null;
            }

            return new MissionInstanceData
            {
                MissionRuntimeId = saveData.MissionRuntimeId,
                TemplateId = saveData.TemplateId,
                Title = saveData.Title,
                Description = saveData.Description,
                MissionType = saveData.MissionType,
                Status = saveData.Status,
                SourceSystemId = saveData.SourceSystemId,
                TargetSystemId = saveData.TargetSystemId,
                Reward = saveData.Reward,
                Objective = saveData.Objective,
                IsReadyToTurnIn = saveData.IsReadyToTurnIn
            };
        }

        public static List<MissionSaveData> ToSaveList(IReadOnlyList<MissionInstanceData> missions)
        {
            List<MissionSaveData> result = new();

            if (missions == null)
            {
                return result;
            }

            foreach (MissionInstanceData mission in missions)
            {
                MissionSaveData saveData = ToSaveData(mission);
                if (saveData != null)
                {
                    result.Add(saveData);
                }
            }

            return result;
        }

        public static List<MissionInstanceData> ToRuntimeList(List<MissionSaveData> saveList)
        {
            List<MissionInstanceData> result = new();

            if (saveList == null)
            {
                return result;
            }

            foreach (MissionSaveData saveData in saveList)
            {
                MissionInstanceData runtimeData = ToRuntimeData(saveData);
                if (runtimeData != null)
                {
                    result.Add(runtimeData);
                }
            }

            return result;
        }
    }