public static class MissionListVisibilityRules
{
    public static bool ShouldShow(MissionInstanceData mission) => mission != null &&
        !(MissionBattleRules.IsBattle(mission.MissionType) &&
          (mission.Status == MissionStatus.Failed || mission.Status == MissionStatus.Expired));
}
