using System;
using UnityEngine;

[Serializable]
public sealed class StationLevelEffectsSettings
{
    [Range(1, 10)] public int Level = 1;
    [Tooltip("Максимальное здоровье в единицах. 0 — 400 + 50 за каждый уровень после первого.")]
    [Min(0)] public int MaxHealth;
    public int EffectiveMaxHealth => MaxHealth > 0 ? MaxHealth : StationInfrastructureRules.DefaultMaxHealth(Level);
    [Tooltip("Дополнительное население военной базы. В правилах допустимы только Military.")]
    public SystemPopulationRule Population;
    [Range(0, 1)] public float RangerAttackProbability;
    [Min(0)] public float ScienceRate;
    [Tooltip("Здоровье за один завершённый игровой тик при отсутствии врагов. 0 — саморемонт отключён.")]
    [Min(0)] public int HealthPerTick = 1;
    [Tooltip("Урон здоровью станции в единицах за шаг вторжения, если защитников и игрока нет.")]
    [Min(0)] public int UnopposedInvasionDamagePerTick = 10;
    [Tooltip("X: доля повреждения 0–1. Y: эффективность 0–1.")]
    public AnimationCurve EfficiencyByDamage = AnimationCurve.Linear(0, 1, 1, 0);
    public StationInstantPaymentOption[] RepairPaymentOptions = Array.Empty<StationInstantPaymentOption>();
    public StationInstantPaymentOption[] RecoveryPaymentOptions = Array.Empty<StationInstantPaymentOption>();
    [Range(1, 10)] public int RequiredGalaxyLevel = 1;
    public float Efficiency(double damage) => Mathf.Clamp01(EfficiencyByDamage == null
        ? 1f - Mathf.Clamp01((float)(damage / 100d)) : EfficiencyByDamage.Evaluate(Mathf.Clamp01((float)(damage / 100d))));
}

[CreateAssetMenu(menuName = "StarFrontier/Configs/Station/Level Effects")]
public sealed class StationLevelEffectsConfig : BaseConfig
{
    public StationLevelEffectsSettings[] Levels = Array.Empty<StationLevelEffectsSettings>();
    public StationLevelEffectsSettings GetLevel(int level)
    {
        StationLevelEffectsSettings found = null;
        if (Levels == null) return null;
        foreach (var entry in Levels)
            if (entry != null && entry.Level == level)
            { if (found != null) return null; found = entry; }
        return found;
    }
}
