using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationInfrastructureAndPaymentsEditModeTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
    private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); _assets.Add(a); return a; }
    private static void Set(object target, string field, object value)
    {
        var type = target.GetType(); FieldInfo info = null;
        while (type != null && info == null) { info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic); type = type.BaseType; }
        Assert.That(info, Is.Not.Null, field); info.SetValue(target, value);
    }
    [TearDown] public void TearDown() { foreach (var a in _assets) UnityEngine.Object.DestroyImmediate(a); _assets.Clear(); }
    private static StarSystemRuntimeState System(int damage = 0) => new StarSystemRuntimeState
    { SystemId = "sys", InfrastructureDamage = damage, InfrastructureDamageState = damage > 0 ? SystemInfrastructureDamageState.Damaged : SystemInfrastructureDamageState.Intact,
        Station = new StationRuntimeState { ConfigId = "station", Level = 2 } };
    private static PlayerState Player()
    {
        var p = new PlayerState { Credits = 100, Diamonds = 10 };
        p.PlayerShipState.ActiveShipId = "ship";
        var ship = new ShipRuntimeData { ShipId = "ship" };
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_ai_01", 5));
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_ai_01", 20) { MissionRuntimeId = "mission" });
        p.PlayerShipState.OwnedShips.Add(ship); return p;
    }
    private static StationPaymentCostSnapshot Cost(StationPaymentResource resource, int amount, string id = "") =>
        new StationPaymentCostSnapshot { Resource = resource, Amount = amount, ItemConfigId = id };
    private static StationOperationRuntimeState Quote(params StationPaymentCostSnapshot[] costs) =>
        new StationOperationRuntimeState { OperationId = "op", PaidCosts = costs };

    [TestCase(1)] [TestCase(7)] [TestCase(1000)]
    public void AutoRepair_UsesHealthPerCompletedTickAndClampsToMaximum(int rate)
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(100, false);
        var settings = new StationLevelEffectsSettings { HealthPerTick = rate };
        StationInfrastructureRules.AutoRepair(system, settings, false);
        Assert.That(system.Station.Health, Is.EqualTo(Math.Min(450, 350 + rate)));
        StationInfrastructureRules.AutoRepair(system, settings, false, int.MaxValue);
        Assert.That(system.Station.Health, Is.EqualTo(450)); Assert.That(system.InfrastructureDamage, Is.Zero);
    }
    [Test] public void AutoRepair_EnemiesDoNotBankHealingForLater()
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(100, false);
        var settings = new StationLevelEffectsSettings { HealthPerTick = 5 };
        StationInfrastructureRules.AutoRepair(system, settings, true, 100);
        Assert.That(system.Station.Health, Is.EqualTo(350));
        StationInfrastructureRules.AutoRepair(system, settings, false);
        Assert.That(system.Station.Health, Is.EqualTo(355));
    }
    [Test] public void AutoRepair_SaveRoundTripKeepsAbsoluteHealth()
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(100, false);
        var settings = new StationLevelEffectsSettings { HealthPerTick = 3 };
        StationInfrastructureRules.AutoRepair(system, settings, false);
        system = JsonUtility.FromJson<StarSystemRuntimeState>(JsonUtility.ToJson(system));
        StationInfrastructureRules.AutoRepair(system, settings, false);
        Assert.That(system.Station.Health, Is.EqualTo(356));
    }
    [Test] public void DestroyedStation_DoesNotSelfRebuild()
    {
        var system = System(100); system.Station.IsDestroyed = true;
        Assert.That(StationInfrastructureRules.AutoRepair(system, new StationLevelEffectsSettings { HealthPerTick = 1000 }, false), Is.False);
        Assert.That(system.Station.IsDestroyed, Is.True);
    }
    [TestCase(0)] [TestCase(-1)]
    public void AutoRepair_NoCompletedTickDoesNotChangeState(int ticks)
    {
        var system = System(50); string before = JsonUtility.ToJson(system);
        StationInfrastructureRules.AutoRepair(system, new StationLevelEffectsSettings(), false, ticks);
        Assert.That(JsonUtility.ToJson(system), Is.EqualTo(before));
    }
    [Test] public void AutoRepair_DisabledOrCapturedStationDoesNotHeal()
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(100, false);
        var settings = new StationLevelEffectsSettings { HealthPerTick = 0 };
        Assert.That(StationInfrastructureRules.AutoRepair(system, settings, false), Is.False);
        settings.HealthPerTick = 5; system.SystemStatus = StarSystemStatus.Captured;
        Assert.That(StationInfrastructureRules.AutoRepair(system, settings, false), Is.False);
        Assert.That(system.Station.Health, Is.EqualTo(350));
    }
    [Test] public void Efficiency_IsCalculatedWithoutAccumulatingBonuses()
    {
        var system = System(40); var settings = new StationLevelEffectsSettings();
        Assert.That(StationInfrastructureRules.Efficiency(system, settings), Is.EqualTo(.6f).Within(.001));
        Assert.That(StationInfrastructureRules.Efficiency(system, settings), Is.EqualTo(.6f).Within(.001));
        system.Station.IsDestroyed = true;
        Assert.That(StationInfrastructureRules.Efficiency(system, settings), Is.Zero);
        system.Station.IsDestroyed = false; system.InfrastructureDamage = 0;
        Assert.That(StationInfrastructureRules.Efficiency(system, settings), Is.EqualTo(1));
    }
    [Test] public void Capture_RequiresDestroyedStationAndNoMilitary()
    {
        var system = System();
        Assert.That(StationInfrastructureRules.CanCapture(system, false), Is.False);
        system.Station.IsDestroyed = true;
        Assert.That(StationInfrastructureRules.CanCapture(system, true), Is.False);
        Assert.That(StationInfrastructureRules.CanCapture(system, false), Is.True);
    }
    [Test] public void DirectCapture_CannotBypassMilitaryGuard()
    {
        var system = System(); system.Station.IsDestroyed = true; system.HasLivingMilitary = () => true;
        system.MarkCaptured();
        Assert.That(system.SystemStatus, Is.Not.EqualTo(StarSystemStatus.Captured));
        system.HasLivingMilitary = () => false; system.MarkCaptured();
        Assert.That(system.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
    }
    [Test] public void ResourceFailure_DoesNotSpendOtherResourcesOrMissionCargo()
    {
        var player = Player(); string before = JsonUtility.ToJson(player);
        Assert.That(StationPaymentRules.TrySpend(player, Quote(Cost(StationPaymentResource.Credits, 10), Cost(StationPaymentResource.Astralites, 6, "item_astralite_ai_01")), out _), Is.False);
        Assert.That(JsonUtility.ToJson(player), Is.EqualTo(before));
    }
    [Test] public void ResourcePayment_CommitsWholeQuoteAndPreservesMissionCargo()
    {
        var player = Player();
        Assert.That(StationPaymentRules.TrySpend(player, Quote(Cost(StationPaymentResource.Credits, 10), Cost(StationPaymentResource.Astralites, 3, "item_astralite_ai_01")), out _), Is.True);
        Assert.That(player.Credits, Is.EqualTo(90));
        Assert.That(player.GetActiveShip().Cargo.GetTradeableQuantity("item_astralite_ai_01"), Is.EqualTo(2));
        Assert.That(player.GetActiveShip().Cargo.entries.Find(x => x.MissionRuntimeId == "mission").Quantity, Is.EqualTo(20));
    }
    [Test] public void AggregatedDuplicateCosts_CannotOverspend()
    {
        var p = Player(); string before = JsonUtility.ToJson(p);
        Assert.That(StationPaymentRules.TrySpend(p, Quote(Cost(StationPaymentResource.Credits, 60), Cost(StationPaymentResource.Credits, 60)), out _), Is.False);
        Assert.That(JsonUtility.ToJson(p), Is.EqualTo(before));
    }
    [Test] public void Diamonds_AreSpentWithoutAnActiveShip()
    {
        var p = new PlayerState { Diamonds = 10 };
        Assert.That(StationPaymentRules.TrySpend(p, Quote(Cost(StationPaymentResource.Diamonds, 7)), out _), Is.True);
        Assert.That(p.Diamonds, Is.EqualTo(3));
    }
    [Test] public void RepairQuote_ScalesCostAndHasNoDuration()
    {
        var config = Asset<StationConfig>(); var profile = Asset<StationLevelEffectsConfig>();
        profile.Levels = new[] { new StationLevelEffectsSettings { Level = 2,
            RepairPaymentOptions = new[] { new StationInstantPaymentOption { Id = "pay", Costs = new[] {
                new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = 100 } } } } } };
        Set(config, "levelEffects", profile);
        Assert.That(StationInfrastructureRules.TryRepairQuote(config, System().Station, 30, "pay", out var quote), Is.True);
        Assert.That(quote.DurationSeconds, Is.Zero);
        Assert.That(quote.PaidCosts[0].Amount, Is.EqualTo(30));
    }
    [Test] public void SourceIdentity_SurvivesCompactSaveRuleIdAndJson()
    {
        var npc = new SystemNpcRuntimeState { NpcType = SystemNpcType.Ally, AllyRole = AllyRole2A.Military,
            SpawnRuleId = StationMilitaryRules.RuleId("home", "base", "rule") };
        npc = JsonUtility.FromJson<SystemNpcRuntimeState>(JsonUtility.ToJson(npc));
        Assert.That(StationMilitaryRules.SourceSystem(npc), Is.EqualTo("home"));
    }
    [Test] public void MilitaryTravel_RejectsCapturedAndOtherSector()
    {
        var home = Asset<StarSystemConfig>(); Set(home, "id", "home");
        var other = Asset<StarSystemConfig>(); Set(other, "id", "other");
        var sector = Asset<SectorConfig>(); Set(sector, "systems", new[] { home, other });
        var npc = new SystemNpcRuntimeState { NpcType = SystemNpcType.Ally, AllyRole = AllyRole2A.Military,
            SpawnRuleId = StationMilitaryRules.RuleId("home", "base", "rule") };
        var galaxy = new GalaxyRuntimeState(); galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "other" });
        Assert.That(StationMilitaryRules.CanTravel(npc, "other", new[] { sector }, galaxy), Is.True);
        galaxy.Systems[0].SystemStatus = StarSystemStatus.Captured;
        Assert.That(StationMilitaryRules.CanTravel(npc, "other", new[] { sector }, galaxy), Is.False);
        Assert.That(StationMilitaryRules.CanTravel(npc, "outside", new[] { sector }, galaxy), Is.False);
    }
    [Test] public void LogChannels_AreDisabledByDefaultAndRespectMasterSwitch()
    {
        var config = Asset<DebugLogConfig>();
        Assert.That(config.IsEnabled(DebugLogChannel.StationEffects), Is.False);
        Assert.That(config.IsEnabled(DebugLogChannel.Research), Is.False);
        Set(config, "stationEffectsLogs", true); Set(config, "researchLogs", true); Set(config, "logsEnabled", true);
        Assert.That(config.IsEnabled(DebugLogChannel.Research), Is.True);
        Assert.That(config.IsEnabled(DebugLogChannel.StationEffects), Is.True);
        Set(config, "logsEnabled", false);
        Assert.That(config.IsEnabled(DebugLogChannel.Research), Is.False);
        Assert.That(config.IsEnabled(DebugLogChannel.StationEffects), Is.False);
    }
    private static PlayerState MixedAstralites(int ai, int ancients, int infected)
    {
        var player = Player(); var cargo = player.GetActiveShip().Cargo; cargo.entries.Clear();
        cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_ai_01", ai));
        cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_ancients_01", ancients));
        cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_infected_01", infected));
        return player;
    }
    [Test] public void AnyAstralite_MixedOneTwoTwo_PaysFiveAndPreviewDoesNotMutate()
    {
        var player = MixedAstralites(1, 2, 2); string before = JsonUtility.ToJson(player);
        var quote = Quote(Cost(StationPaymentResource.Credits, 10), Cost(StationPaymentResource.Astralites, 5, "item_astralite_ai_01"));
        Assert.That(StationPaymentRules.GetSpendableAstralites(player), Is.EqualTo(5));
        Assert.That(StationPaymentRules.CanAfford(player, quote, out _), Is.True); Assert.That(JsonUtility.ToJson(player), Is.EqualTo(before));
        Assert.That(StationPaymentRules.TrySpend(player, quote, out _), Is.True);
        Assert.That(player.Credits, Is.EqualTo(90)); Assert.That(StationPaymentRules.GetSpendableAstralites(player), Is.Zero);
    }
    [TestCase("item_astralite_ai_01")] [TestCase("item_astralite_ancients_01")] [TestCase("item_astralite_infected_01")]
    public void AnySingleSpecies_CanPayLegacyAiQuote(string itemId)
    {
        var player = MixedAstralites(0, 0, 0); player.GetActiveShip().Cargo.entries.Add(new RuntimeCargoEntryData(itemId, 5));
        Assert.That(StationPaymentRules.TrySpend(player, Quote(Cost(StationPaymentResource.Astralites, 5, "item_astralite_ai_01")), out _), Is.True);
        Assert.That(StationPaymentRules.GetSpendableAstralites(player), Is.Zero);
    }
    [Test] public void CombinedShortage_DoesNotUseMissionCargoOrOtherGoods_AndDoesNotSpendCredits()
    {
        var player = MixedAstralites(1, 1, 2); var cargo = player.GetActiveShip().Cargo;
        cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_ai_01", 30) { MissionRuntimeId = "delivery" });
        cargo.entries.Add(new RuntimeCargoEntryData("ore", 100)); string before = JsonUtility.ToJson(player);
        var quote = Quote(Cost(StationPaymentResource.Credits, 10), Cost(StationPaymentResource.Astralites, 5));
        Assert.That(StationPaymentRules.GetSpendableAstralites(player), Is.EqualTo(4));
        Assert.That(StationPaymentRules.CanAfford(player, quote, out _), Is.False);
        Assert.That(StationPaymentRules.TrySpend(player, quote, out _), Is.False); Assert.That(JsonUtility.ToJson(player), Is.EqualTo(before));
    }
    [Test] public void PartialMixedPayment_PreservesSurplusAndMissionCargo()
    {
        var player = MixedAstralites(1, 2, 5); var cargo = player.GetActiveShip().Cargo;
        cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_infected_01", 7) { MissionRuntimeId = "delivery" });
        cargo.entries.Add(new RuntimeCargoEntryData("ore", 4));
        Assert.That(StationPaymentRules.TrySpend(player, Quote(Cost(StationPaymentResource.Astralites, 5)), out _), Is.True);
        Assert.That(StationPaymentRules.GetSpendableAstralites(player), Is.EqualTo(3));
        Assert.That(cargo.entries.Find(x => x.MissionRuntimeId == "delivery").Quantity, Is.EqualTo(7));
        Assert.That(cargo.GetTradeableQuantity("ore"), Is.EqualTo(4));
    }
    [Test] public void MultipleAstraliteCosts_AreAggregatedAgainstOnePool()
    {
        var player = MixedAstralites(1, 2, 2); string before = JsonUtility.ToJson(player);
        var quote = Quote(Cost(StationPaymentResource.Astralites, 3, "item_astralite_ai_01"),
            Cost(StationPaymentResource.Astralites, 3, "item_astralite_ancients_01"));
        Assert.That(StationPaymentRules.TrySpend(player, quote, out _), Is.False); Assert.That(JsonUtility.ToJson(player), Is.EqualTo(before));
    }
    [Test] public void GenericAstraliteConfig_DoesNotRequireSpeciesItemReference()
    {
        var option = new StationPaymentOption { Id = "ordinary", Costs = new[] {
            new StationPaymentCost { Resource = StationPaymentResource.Credits, Amount = 10 },
            new StationPaymentCost { Resource = StationPaymentResource.Astralites, Amount = 5 } } };
        Assert.That(option.TryCreateSnapshot(StationOperationKind.Construction, 1, out var quote), Is.True);
        Assert.That(quote.PaidCosts[1].ItemConfigId, Is.Empty);
        Assert.That(StationPaymentRules.TrySpend(MixedAstralites(1, 2, 2), quote, out _), Is.True);
    }


    [TestCase(1, 400)] [TestCase(2, 450)] [TestCase(3, 500)] [TestCase(4, 550)]
    [TestCase(5, 600)] [TestCase(6, 650)] [TestCase(7, 700)] [TestCase(8, 750)]
    [TestCase(9, 800)] [TestCase(10, 850)]
    public void AbsoluteHealth_LevelDefinesMaximum_AndDamageUsesUnits(int level, int maximum)
    {
        var system = System(); system.Station.Level = level;
        system.ApplyInfrastructureDamageFromWar(50, false);
        Assert.That(system.Station.MaxHealth, Is.EqualTo(maximum));
        Assert.That(system.Station.Health, Is.EqualTo(maximum - 50));
        Assert.That(system.Station.IsDestroyed, Is.False);
        system.ApplyInfrastructureDamageFromWar(maximum - 51, false);
        Assert.That(system.Station.Health, Is.EqualTo(1));
        Assert.That(system.InfrastructureDamage, Is.LessThan(100));
        system.ApplyInfrastructureDamageFromWar(1, false);
        Assert.That(system.Station.Health, Is.Zero); Assert.That(system.Station.IsDestroyed, Is.True);
        Assert.That(system.InfrastructureDamage, Is.EqualTo(100));
    }
    [Test] public void AbsoluteHealth_OldSaveKeepsDamageFraction_AndMigrationIsIdempotent()
    {
        var system = System(40); // Level 2: 450 total, 180 missing.
        StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Health, Is.EqualTo(270));
        Assert.That(system.Station.MaxHealth, Is.EqualTo(450));
        string json = JsonUtility.ToJson(system);
        long revision = system.Station.Revision;
        for (int reload = 0; reload < 5; reload++)
        {
            system = JsonUtility.FromJson<StarSystemRuntimeState>(JsonUtility.ToJson(system));
            StationLifecycleRules.Normalize(system);
            Assert.That(JsonUtility.ToJson(system), Is.EqualTo(json), "Reload " + reload);
            Assert.That(system.Station.Revision, Is.EqualTo(revision));
            Assert.That(system.Station.Health, Is.EqualTo(270));
        }
        Assert.That(system.InfrastructureDamage, Is.EqualTo(40));
    }
    [Test] public void AbsoluteHealth_DestroyedLegacySaveStaysDestroyed()
    {
        var system = System(100); system.Station.IsDestroyed = true;
        StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Health, Is.Zero); Assert.That(system.Station.IsDestroyed, Is.True);
    }
    [Test] public void AbsoluteHealth_UpgradeWaitsForFullRepairBeforeGrantingCapacity()
    {
        var system = System(); system.Station.Level = 1;
        Assert.That(StationLifecycleRules.BeginPaidOperation(system.Station, new StationOperationRuntimeState {
            Kind = StationOperationKind.Upgrade, TargetLevel = 2, DurationSeconds = 1, OperationId = "health-upgrade" }), Is.True);
        system.ApplyInfrastructureDamageFromWar(100, false);
        StationLifecycleRules.Advance(system, 100);
        Assert.That(system.Station.Level, Is.EqualTo(1)); Assert.That(system.Station.Development.ElapsedSeconds, Is.Zero);
        StationInfrastructureRules.AutoRepair(system, new StationLevelEffectsSettings { HealthPerTick = 100 }, false);
        StationLifecycleRules.Advance(system, 1);
        Assert.That(system.Station.Level, Is.EqualTo(2)); Assert.That(system.Station.MaxHealth, Is.EqualTo(450));
        Assert.That(system.Station.Health, Is.EqualTo(450));
    }
    [Test] public void AbsoluteHealth_CustomLevelSettingIsRespected()
    {
        var system = System(); StationInfrastructureRules.EnsureHealth(system, 1200);
        system.ApplyInfrastructureDamageFromWar(50, false); StationLifecycleRules.Normalize(system);
        Assert.That(system.Station.Health, Is.EqualTo(1150)); Assert.That(system.Station.MaxHealth, Is.EqualTo(1200));
    }
    [Test] public void AbsoluteHealth_SubPercentDamageIsRepairedAndChangesEfficiency()
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(1, false);
        Assert.That(system.InfrastructureDamage, Is.EqualTo(1));
        Assert.That(StationInfrastructureRules.Efficiency(system, new StationLevelEffectsSettings()),
            Is.EqualTo(449f / 450f).Within(.00001));
        system = JsonUtility.FromJson<StarSystemRuntimeState>(JsonUtility.ToJson(system));
        StationInfrastructureRules.AutoRepair(system, new StationLevelEffectsSettings { HealthPerTick = 1 }, true);
        Assert.That(system.Station.Health, Is.EqualTo(449));
        Assert.That(StationInfrastructureRules.AutoRepair(system, new StationLevelEffectsSettings { HealthPerTick = 1 }, false), Is.True);
        Assert.That(system.Station.Health, Is.EqualTo(450)); Assert.That(system.InfrastructureDamage, Is.Zero);
    }
    [Test] public void AbsoluteHealth_RecoveryRestoresFullCapacityAndRetainsLevel()
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(1, true);
        StationLifecycleRules.BeginPaidOperation(system.Station, new StationOperationRuntimeState {
            Kind = StationOperationKind.Recovery, TargetLevel = 2, OperationId = "health-recovery" });
        StationLifecycleRules.Advance(system, 0);
        Assert.That(system.Station.Health, Is.EqualTo(450)); Assert.That(system.Station.Level, Is.EqualTo(2));
        Assert.That(system.Station.IsDestroyed, Is.False); Assert.That(system.InfrastructureDamage, Is.Zero);
    }

    [TestCase("trade", 500, 2, 25)] [TestCase("ranger_base", 600, 3, 30)]
    [TestCase("military", 700, 4, 35)] [TestCase("science", 800, 5, 40)] [TestCase("medical", 900, 6, 45)]
    public void RecoveryPrices_UseStationTypeAndReachedLevel_ForBothPaymentChoices(string kind, int credits, int astralites, int diamonds)
    {
        var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<StationLevelEffectsConfig>(
            "Assets/_Project/Content/Configs/Stations2A/Levels/station_levels_" + kind + ".asset");
        Assert.That(profile, Is.Not.Null);
        var config = Asset<StationConfig>(); Set(config, "levelEffects", profile);
        for (int level = 1; level <= 10; level++)
        {
            Assert.That(config.TryCreateOperation(StationOperationKind.Recovery, level, "credits_astralites", out var ordinary), Is.True);
            Assert.That(config.TryCreateOperation(StationOperationKind.Recovery, level, "diamonds", out var accelerated), Is.True);
            Assert.That(ordinary.TargetLevel, Is.EqualTo(level)); Assert.That(accelerated.TargetLevel, Is.EqualTo(level));
            Assert.That(ordinary.DurationSeconds, Is.Zero); Assert.That(accelerated.DurationSeconds, Is.Zero);
            Assert.That(ordinary.PaidCosts.Length, Is.EqualTo(2));
            Assert.That(Array.Find(ordinary.PaidCosts, c => c.Resource == StationPaymentResource.Credits).Amount, Is.EqualTo(credits * level));
            Assert.That(Array.Find(ordinary.PaidCosts, c => c.Resource == StationPaymentResource.Astralites).Amount, Is.EqualTo(astralites * level));
            Assert.That(accelerated.PaidCosts.Length, Is.EqualTo(1));
            Assert.That(accelerated.PaidCosts[0].Resource, Is.EqualTo(StationPaymentResource.Diamonds));
            Assert.That(accelerated.PaidCosts[0].Amount, Is.EqualTo(diamonds * level));
        }
    }

    [Test] public void AutoRepair_EmptySerializedRecoveryDoesNotBlockHealingOrAddExtraRevision()
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(1, false);
        system.Station.Development = new StationOperationRuntimeState();
        system.Station.Recovery = new StationOperationRuntimeState();
        long revision = system.Station.Revision;
        Assert.That(StationInfrastructureRules.AutoRepair(system, new StationLevelEffectsSettings { HealthPerTick = 1 }, false), Is.True);
        Assert.That(system.Station.Health, Is.EqualTo(450));
        Assert.That(system.Station.Development, Is.Null); Assert.That(system.Station.Recovery, Is.Null);
        Assert.That(system.Station.Revision, Is.EqualTo(revision + 1));
    }
    [Test] public void AutoRepair_RealPaidRecoveryIsNeverDiscardedOrHealedAutomatically()
    {
        var system = System(); system.ApplyInfrastructureDamageFromWar(1, true);
        var recovery = new StationOperationRuntimeState { Kind = StationOperationKind.Recovery,
            OperationId = "paid-recovery", TargetLevel = 2, DurationSeconds = 10, ElapsedSeconds = 3 };
        system.Station.Recovery = recovery;
        Assert.That(StationInfrastructureRules.AutoRepair(system, new StationLevelEffectsSettings { HealthPerTick = 100 }, false), Is.False);
        Assert.That(system.Station.Recovery, Is.SameAs(recovery));
        Assert.That(system.Station.Health, Is.Zero); Assert.That(recovery.ElapsedSeconds, Is.EqualTo(3));
    }
}
