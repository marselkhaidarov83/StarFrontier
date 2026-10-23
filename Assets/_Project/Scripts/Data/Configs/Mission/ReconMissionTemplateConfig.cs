using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ReconDistanceWeight
{
    [Min(1)] public int EnemyJumps;
    [Min(0)] public float Weight;
}

[CreateAssetMenu(fileName = "ReconMissionTemplateConfig", menuName = "StarFrontier/Configs/Mission/Recon Template")]
public sealed class ReconMissionTemplateConfig : MissionTemplateConfig
{
    public override MissionType MissionType => global::MissionType.Recon;
    public IReadOnlyList<ReconDistanceWeight> DistanceWeights => distanceWeights;
    [Header("Разведка вражеских систем")]
    [Tooltip("Относительные веса дальности после последней союзной системы. Сначала выбирается дальность, затем система внутри выбранной группы.")]
    [SerializeField] private List<ReconDistanceWeight> distanceWeights;
    [Tooltip("Множитель веса каждого следующего прыжка после последней явно заданной дальности. 0 исключает более далёкие цели.")]
    [SerializeField] [Range(0, 1)] private float furtherJumpWeightMultiplier;
    [Tooltip("Не предлагать цели за ещё недоступными переходами и секторами.")]
    [SerializeField] private bool requireUnlockedRoutes;
    [Header("Сближение с врагами")]
    [Tooltip("Допустимый радиус от корабля до каждого отдельного живого врага. Позже определяется радаром.")]
    [SerializeField] [Min(0)] private float scanDistance;
    [SerializeField] [Min(1)] private int minimumEnemyCount;
    [SerializeField] [Min(1)] private int maximumEnemyCount;
    [Tooltip("Доля врагов в системе, исключаемая из доступного числа целей при выдаче.")]
    [SerializeField] [Range(0, 100)] private int enemyReservePercent;
    public bool RequireUnlockedRoutes => requireUnlockedRoutes;
    public float ScanDistance => scanDistance;
    public int MinimumEnemyCount => minimumEnemyCount;
    public int MaximumEnemyCount => maximumEnemyCount;
    public int EnemyReservePercent => enemyReservePercent;

    public double GetDistanceWeight(int enemyJumps)
    {
        if (enemyJumps <= 0 || distanceWeights == null) return 0;
        ReconDistanceWeight last = null;
        foreach (var entry in distanceWeights)
        {
            if (entry == null || entry.EnemyJumps <= 0 || float.IsNaN(entry.Weight) || float.IsInfinity(entry.Weight)) continue;
            if (entry.EnemyJumps == enemyJumps) return Math.Max(0, entry.Weight);
            if (last == null || entry.EnemyJumps > last.EnemyJumps) last = entry;
        }
        if (last == null || enemyJumps <= last.EnemyJumps ||
            float.IsNaN(furtherJumpWeightMultiplier) || furtherJumpWeightMultiplier < 0 || furtherJumpWeightMultiplier > 1) return 0;
        return Math.Max(0, last.Weight) * Math.Pow(furtherJumpWeightMultiplier, enemyJumps - last.EnemyJumps);
    }
}
