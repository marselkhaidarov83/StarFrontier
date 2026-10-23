using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ReconMissionSurveyRules
{
    public static int MaxTargets(int availableEnemies, int reservePercent)
    {
        if (availableEnemies <= 0 || reservePercent < 0 || reservePercent > 100) return 0;
        return (int)((long)availableEnemies * (100 - reservePercent) / 100);
    }

    public static int PickRequired(int availableEnemies, ReconMissionTemplateConfig config, double roll)
    {
        if (config == null || config.MinimumEnemyCount <= 0 || config.MaximumEnemyCount < config.MinimumEnemyCount) return 0;
        int maximum = Math.Min(config.MaximumEnemyCount, MaxTargets(availableEnemies, config.EnemyReservePercent));
        if (maximum < config.MinimumEnemyCount) return 0;
        int count = maximum - config.MinimumEnemyCount + 1;
        return config.MinimumEnemyCount + Math.Min(count - 1, (int)(Math.Max(0, Math.Min(1, roll)) * count));
    }

    public static List<string> FindNewNearbyEnemyIds(IReadOnlyList<SystemNpcRuntimeState> enemies,
        ICollection<string> observed, Vector3 playerPosition, float distance, int remaining)
    {
        var result = new List<string>();
        if (enemies == null || remaining <= 0 || distance <= 0 || float.IsNaN(distance) || float.IsInfinity(distance)) return result;
        float distanceSquared = distance * distance;
        foreach (var enemy in enemies.Where(e => e != null && e.IsEnemy && e.IsAvailableForCombat() &&
                     !string.IsNullOrWhiteSpace(e.RuntimeNpcId)).OrderBy(e => e.RuntimeNpcId, StringComparer.Ordinal))
        {
            if (result.Count >= remaining) break;
            if (observed != null && observed.Contains(enemy.RuntimeNpcId)) continue;
            if ((enemy.CurrentPosition - playerPosition).sqrMagnitude <= distanceSquared)
                result.Add(enemy.RuntimeNpcId);
        }
        return result;
    }
}
