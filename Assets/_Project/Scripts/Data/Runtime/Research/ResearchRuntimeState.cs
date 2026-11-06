using System;
using System.Collections.Generic;

// Stable serialized values. Add new IDs at the end; do not renumber shipped upgrades.
public enum ResearchUpgrade
{
    None = 0, MilitarySuperiority = 1, TradeSuperiority = 2,
    MilitaryDefence = 3, MilitaryProduction = 4, MilitaryCruisers = 5,
    TradeSupply = 6, TradeEconomy = 7, ScienceComputing = 8, ScienceQuantumLabs = 9,
    ScienceExploration = 10, MedicalRestoration = 11, MedicalBiotech = 12, MedicalPrevention = 13,
    RangerTraining = 14, RangerPatrols = 15, RangerMastery = 16
}
[Serializable] public sealed class ResearchUpgradeLevel
{
    public ResearchUpgrade Upgrade;
    public int Level;
}
[Serializable] public sealed class ResearchRuntimeState
{
    public List<ResearchUpgradeLevel> Levels = new List<ResearchUpgradeLevel>();
    public StationOperationRuntimeState Active;
    public ResearchUpgrade ActiveUpgrade;
    public long Revision;
    public int GetLevel(ResearchUpgrade upgrade) => Levels?.Find(x => x != null && x.Upgrade == upgrade)?.Level ?? 0;
    public void Normalize()
    {
        Levels ??= new List<ResearchUpgradeLevel>();
        if (Active != null && string.IsNullOrEmpty(Active.OperationId) && Active.Kind == StationOperationKind.None &&
            Active.DurationSeconds == 0 && Active.ElapsedSeconds == 0 && (Active.PaidCosts == null || Active.PaidCosts.Length == 0)) Active = null;
    }
}
