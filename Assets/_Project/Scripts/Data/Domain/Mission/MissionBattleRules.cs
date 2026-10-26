using System;

public static class MissionBattleRules
{
    public static bool IsBattle(MissionType type) => type == MissionType.Defense || type == MissionType.Liberation;
    public static int Credits(int kills, int deflator) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, kills) * Math.Max(0, deflator));
    public static bool RewardExpired(MissionObjectiveData objective, int generation) =>
        objective != null && objective.BattleWon && generation > objective.BattleInvasionGeneration;
    public static int RouteLevel(int level) => Math.Max(1, Math.Min(5, level));
}
