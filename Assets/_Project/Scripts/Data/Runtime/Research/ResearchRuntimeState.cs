using System;
using System.Collections.Generic;

// Stable serialized values. Add new IDs at the end; do not renumber shipped upgrades.
public enum ResearchUpgrade
{
    None = 0, MilitarySuperiority = 1, TradeSuperiority = 2,
    MilitaryDefence = 3, MilitaryProduction = 4, MilitaryCruisers = 5,
    TradeSupply = 6, TradeEconomy = 7, ScienceComputing = 8, ScienceQuantumLabs = 9,
    ScienceExploration = 10, MedicalRestoration = 11, MedicalBiotech = 12, MedicalPrevention = 13,
    RangerTraining = 14, RangerPatrols = 15, RangerMastery = 16,
    EconomyWarProtection = 17, EquipmentBuyout = 18,
    MilitaryFireControl = 19,
    MilitaryAmmunition = 20,
    MilitaryCombatPower = 21,
    MilitaryMissileDefense = 22,
    MilitaryElectronicWarfare = 23,
    MilitaryFleetCoordination = 24,
    TradeNewGoods = 25,
    TradeWholesale = 26,
    TradeWarehouseReserves = 27,
    TradeCargoInsurance = 28,
    TradeTravelSavings = 29,
    ScienceSpectralAnalysis = 30,
    SciencePlanetaryMapping = 31,
    ScienceAnomalies = 32,
    ScienceEnergyMaterials = 33,
    ScienceExperimentalEngines = 34,
    ScienceLabAutomation = 35,
    ScienceObservatoryNetwork = 36,
    MedicalEmergencyCare = 37,
    MedicalCryostabilization = 38,
    MedicalRadiationProtection = 39,
    MedicalTissueRegeneration = 40,
    MedicalNanotechnology = 41,
    MedicalToxicology = 42,
    MedicalOrbitalClinics = 43,
    RangerBorderWatch = 44,
    RangerRapidResponse = 45,
    RangerSmallUnitTactics = 46,
    RangerPatrolAutonomy = 47,
    RangerFieldRepair = 48,
    RangerSearchAndRescue = 49,
    RangerBaseNetwork = 50,
    MissionDelivery = 51,
    MissionDefense = 52,
    MissionLiberation = 53,
    MissionRouteUnlock = 54,
    MissionRouteUpgrade = 55,
    MissionRecon = 56,
    MissionEscort = 57,
    MissionCrewRescue = 58,
    MissionCargoRecovery = 59,
    MissionInterception = 60,
    TradeMarketImprovement = 61,
    ScienceLaboratories = 62,
    ScienceResearchAcceleration = 63,
    ScienceResearchCostOptimization = 64,
    ScienceInvasionForecast = 65,
    ScienceFuelProbes = 66,
    ScienceFuelGeology = 67,
    ScienceForcedExtraction = 68,
    ScienceFuelContainers = 69,
    ScienceAiStudy = 70,
    ScienceAncientsStudy = 71,
    ScienceInfectedStudy = 72,
    ScienceTrophyRecycling = 73,
    RangerCombatTraining = 74,
    RangerDefensiveSystems = 75,
    RangerHullReinforcement = 76,
    RangerAntiInvasionTraining = 77,
    RangerLongRangeWatch = 78,
    RangerThreatAssessment = 79,
    RangerDefencePriority = 80,
    RangerStationFortification = 81,
    RangerInfrastructureDefence = 82,
    MilitaryFirepower = 83,
    MilitaryWeaponRange = 84,
    MilitaryShieldDamage = 85,
    MilitaryHullReinforcement = 86,
    MilitaryEmergencyArmour = 87,
    MilitaryShieldReinforcement = 88,
    MilitaryCombatReconnaissance = 89,
    MilitaryDefensiveDoctrine = 90,
    MilitaryAssaultDoctrine = 91,
    MilitaryWarTrophies = 92,
    MissionVariety = 93,
    MissionOfferExpansion = 94,
    MissionOfferRefresh = 95,
    MissionParallelLimit = 96,
    MissionCreditReward = 97,
    MissionGeography = 98,
    MissionAdvancePayment = 99,
    MissionGalacticBoard = 100,
    TradeInterstellarSupply = 101,
    TradePersonalWarehouseReserves = 102,
    TradeMarketAnalytics = 103,
    TradeFuelWonder = 104,
    TradeRepairWonder = 105,
    TradeAssortmentRefresh = 106,
    TradeMarketReserves = 107,
    TradeSpecialOffers = 108
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
