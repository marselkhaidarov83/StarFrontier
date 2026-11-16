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
    [Tooltip("Варианты оплаты мгновенного восстановления разрушенной станции на этом уровне.")]
    public StationPaymentOptionsConfig RecoveryPayment;
    [Range(1, 10)] public int RequiredGalaxyLevel = 1;
}
