using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class S08_02_ExplicitDebugCommandGateTests
{
    [TestCase(false, false, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, true, false, false)]
    [TestCase(false, true, true, false)]
    [TestCase(true, false, false, false)]
    [TestCase(true, false, true, true)]
    [TestCase(true, true, false, true)]
    [TestCase(true, true, true, true)]
    public void ExplicitCommand_EditorPlayModeWorksWithoutDebugOverlay(bool playing, bool editor, bool developerMode, bool allowed)
    {
        Assert.That(DeveloperModeGate.CanRunExplicitCommand(playing, editor, developerMode), Is.EqualTo(allowed));
    }
}

public sealed class S08_02_MissionVisibilityEditModeTests
{
    [Test]
    public void ListVisibilityMatrix_HidesOnlyFailedAndExpiredDefenseAndLiberation()
    {
        foreach (MissionType type in Enum.GetValues(typeof(MissionType)))
        foreach (MissionStatus status in Enum.GetValues(typeof(MissionStatus)))
        {
            var mission = new MissionInstanceData { MissionType = type, Status = status };
            bool hidden = (type == MissionType.Defense || type == MissionType.Liberation) &&
                (status == MissionStatus.Failed || status == MissionStatus.Expired);
            Assert.That(MissionListVisibilityRules.ShouldShow(mission), Is.EqualTo(!hidden), type + "/" + status);
        }
        Assert.That(MissionListVisibilityRules.ShouldShow(null), Is.False);
    }

    [Test]
    public void HidingAnArchivedBattle_DoesNotRemoveItsSavedHistory()
    {
        var mission = new MissionInstanceData { MissionRuntimeId = "failed", MissionType = MissionType.Defense, Status = MissionStatus.Failed };
        var block = new RuntimeMissionSaveBlock(); block.ArchivedMissions.Add(mission);
        var visible = block.ArchivedMissions.Where(MissionListVisibilityRules.ShouldShow).ToList();
        Assert.That(visible, Is.Empty);
        Assert.That(block.ArchivedMissions.Single(), Is.SameAs(mission));
    }
}

public sealed partial class S08_02_BattleAndRouteEditModeTests
{
    [Test]
    public void Delivery_AcceptReservesPhysicalCargoAndOnlyCorrectPlanetCanPayOnce()
    {
        var mission = DeliveryOffer(); var ship = _state.Player.GetActiveShip();
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("goods", 2));
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        var inventory = _registry.Get<IInventoryService>();
        Assert.That(inventory.GetUsedCargo(), Is.EqualTo(14));
        Assert.That(inventory.RemoveItem("goods", 3), Is.False, "The mission stack must not be consumed by normal trade.");
        Assert.That(ship.Cargo.entries.Single(e => e.MissionRuntimeId == mission.MissionRuntimeId).Quantity, Is.EqualTo(5));
        _state.Player.CurrentSystemId = "B"; _state.Player.CurrentPlanetId = "wrong";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        _state.Player.CurrentPlanetId = "planet_B";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(inventory.GetUsedCargo(), Is.EqualTo(4));
        Assert.That(_state.Player.Credits, Is.EqualTo(200));
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds.Count, Is.EqualTo(1));
    }

    [Test]
    public void Delivery_PartialCargoLossFailsWithoutRemovingOrdinaryGoodsOrPaying()
    {
        var mission = DeliveryOffer(); var ship = _state.Player.GetActiveShip();
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("goods", 2));
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        ship.Cargo.entries.Single(e => e.MissionRuntimeId == mission.MissionRuntimeId).Quantity--;
        _missions.ReevaluateActiveMissions("CargoLost");
        Assert.That(mission.Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(ship.Cargo.GetQuantity("goods"), Is.EqualTo(2));
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [TestCase(true)] [TestCase(false)]
    public void Delivery_RejectsInsufficientCapacityOrNewlyCapturedDestination(bool insufficientCapacity)
    {
        var mission = DeliveryOffer();
        if (insufficientCapacity) _state.Player.GetActiveShip().CargoCapacity = 9;
        else _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_missions.GetAvailableMissions().Single(), Is.SameAs(mission));
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_state.Player.GetActiveShip().Cargo.entries, Is.Empty);
        Assert.That(_saving, Is.True);
    }

    [Test]
    public void Delivery_SaveLoadKeepsItsCargoBindingAndCanComplete()
    {
        var mission = DeliveryOffer(); Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        ReloadState();
        _state.Player.CurrentSystemId = "B"; _state.Player.CurrentPlanetId = "planet_B";
        _missions.ReevaluateActiveMissions("AfterLoad");
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Player.Credits, Is.EqualTo(200));
        Assert.That(_state.Player.GetActiveShip().Cargo.entries, Is.Empty);
    }

    [Test]
    public void Recon_EntryAloneDoesNotCountAndEachLivingEnemyIsSurveyedOnlyOnceInRange()
    {
        var mission = ReconOffer(); Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Player.CurrentSystemId = "B"; _state.Player.CurrentPlanetId = "";
        _missions.ReevaluateActiveMissions("SystemEntered");
        Assert.That(mission.Objective.CurrentAmount, Is.Zero);
        var near = Enemy("near", 10); var far = Enemy("far", 41);
        var dead = Enemy("dead", 5); dead.IsAlive = false; dead.LifeState = SystemNpcLifeState.Destroyed;
        var landed = Enemy("landed", 5); landed.IsOnPlanet = true;
        _missions.ObserveReconTargets(); _missions.ObserveReconTargets();
        Assert.That(mission.Objective.CurrentAmount, Is.EqualTo(1));
        Assert.That(mission.Objective.ReconObservedEnemyIds, Is.EquivalentTo(new[] { near.RuntimeNpcId }));
        far.CurrentPosition = new Vector3(40, 0, 0);
        _missions.ObserveReconTargets();
        Assert.That(mission.Objective.CurrentAmount, Is.EqualTo(2));
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "another_planet";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        _state.Player.CurrentPlanetId = "planet_A";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Player.Credits, Is.EqualTo(150));
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
    }

    [Test]
    public void Recon_SaveLoadPreservesSurveyedIdsAndDoesNotCountEnemyAgain()
    {
        var mission = ReconOffer(); Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Player.CurrentSystemId = "B"; _state.Player.CurrentPlanetId = ""; Enemy("one", 10);
        _missions.ObserveReconTargets(); ReloadState(); _missions.ObserveReconTargets();
        var loaded = _missions.GetActiveMissions().Single();
        Assert.That(loaded.Objective.CurrentAmount, Is.EqualTo(1));
        Enemy("two", 10); _missions.ObserveReconTargets();
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "planet_A";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Player.Credits, Is.EqualTo(150));
    }

    [Test]
    public void Recon_RejectsAcceptanceIfTargetIsNoLongerHostile()
    {
        var mission = ReconOffer(); _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Stable;
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
    }

    [Test]
    public void ReconGenerator_RespectsLiveEnemyReserveAndConfiguredRequiredCount()
    {
        _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "planet_A";
        var template = Config<ReconMissionTemplateConfig>("recon_template");
        SetBaseTemplate(template, 150); SetField(template, "scanDistance", 40f);
        SetField(template, "minimumEnemyCount", 3); SetField(template, "maximumEnemyCount", 3);
        SetField(template, "enemyReservePercent", 10); SetField(template, "requireUnlockedRoutes", true);
        SetField(template, "distanceWeights", new List<ReconDistanceWeight> { new ReconDistanceWeight { EnemyJumps = 1, Weight = 1 } });
        MissionPool(template); Enemy("e1", 5); Enemy("e2", 5); Enemy("e3", 5);
        var generator = new PlanetMissionOfferGenerator();
        Assert.That(generator.TryGenerateOffer(_systems[0], _testPlanets[0], _missions.GetActiveMissions(), null), Is.Null);
        Enemy("e4", 5);
        var offer = generator.TryGenerateOffer(_systems[0], _testPlanets[0], _missions.GetActiveMissions(), null);
        Assert.That(offer, Is.Not.Null);
        Assert.That(offer.OfferedMission.TargetSystemId, Is.EqualTo("B"));
        Assert.That(offer.OfferedMission.Objective.RequiredAmount, Is.EqualTo(3));
        Assert.That(offer.OfferedMission.Objective.ReconScanDistance, Is.EqualTo(40));
    }

    [TestCase(false)] [TestCase(true)]
    public void Battle_AllPersonalLastHitsDetermineRewardAndEarlyVictorySignalCannotPay(bool liberation)
    {
        if (liberation) _state.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Captured;
        else _state.Galaxy.Invasions.Add(new InvasionState { InvasionId = "invasion", TargetSystemId = "A", LifecycleState = InvasionLifecycleState.Active });
        _encounter.StartEncounter("three", "A", 3, 2);
        _missions.RecordBattleKill("e1:10", "A"); _encounter.RegisterEnemyDestroyed(true);
        _missions.ResolveBattle("three", "A", true);
        Assert.That(_missions.GetActiveMissions().Single().Objective.BattleWon, Is.False);
        _missions.RecordBattleKill("e2:11", "A"); _encounter.RegisterEnemyDestroyed(true);
        _encounter.RegisterEnemyDestroyed(false);
        var mission = _missions.GetActiveMissions().Single();
        _state.Player.CurrentPlanetId = "planet_A";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Player.Credits, Is.EqualTo(liberation ? 350 : 250));
    }

    [Test]
    public void Battle_FirstPlayerDamageCanCreateEncounterBeforeEnemyAttacksPlayer()
    {
        _state.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Captured;
        Enemy("enemy", 5, "A");
        Assert.That(_encounter.Current, Is.Null);
        _missions.ObserveBattleParticipation("A");
        Assert.That(_encounter.HasActiveEncounter, Is.True);
        Assert.That(_encounter.Current.EnemiesAlive, Is.EqualTo(1));
        Assert.That(_missions.GetActiveMissions().Single().Objective.CurrentAmount, Is.Zero);
    }

    [Test]
    public void Route_RewardOverflowDoesNotUnlockRouteOrWriteRewardLedger()
    {
        var mission = AcceptRoute(false); Hop("A", "C"); Hop("C", "B"); Hop("B", "C"); Hop("C", "A");
        _state.Player.CurrentPlanetId = "issuer"; _state.Player.Credits = int.MaxValue;
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_state.Galaxy.Routes.Find(r => r.RouteId == "AB").IsUnlocked, Is.False);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
        Assert.That(_saving, Is.True);
    }

    [Test]
    public void Route_PartialIndirectTripSurvivesSaveLoad()
    {
        var mission = AcceptRoute(false); Hop("A", "C"); ReloadState();
        Hop("C", "B"); Hop("B", "C"); Hop("C", "A"); _state.Player.CurrentPlanetId = "issuer";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Galaxy.Routes.Find(r => r.RouteId == "AB").IsUnlocked, Is.True);
    }

    [Test]
    public void RouteGenerator_UsesCurrentFixedRewardAndDoesNotOfferInaccessibleSector()
    {
        var template = Config<RouteMissionTemplateConfig>("route_template"); SetBaseTemplate(template, 321);
        MissionPool(template);
        var generator = new PlanetMissionOfferGenerator();
        var offer = generator.TryGenerateOffer(_systems[0], _testPlanets[0], _missions.GetActiveMissions(), null);
        Assert.That(offer?.OfferedMission.Objective.RouteId, Is.EqualTo("AB"));
        Assert.That(offer.OfferedMission.Reward.Credits, Is.EqualTo(321));
        _state.Galaxy.Sectors[0].IsUnlocked = false;
        Assert.That(generator.TryGenerateOffer(_systems[0], _testPlanets[0], _missions.GetActiveMissions(), null), Is.Null);
    }

    [Test]
    public void DeliveryGenerator_UsesActualFuelAndConfiguredCargoVolume()
    {
        PrepareCargo(30);
        var template = Config<DeliveryMissionTemplateConfig>("delivery_template"); SetBaseTemplate(template, 0);
        SetField(template, "creditRewardMode", MissionCreditRewardMode.FuelDistance);
        SetField(template, "fuelRewardDeflator", 50f);
        SetField(template, "minimumQuantity", 3); SetField(template, "maximumQuantity", 3);
        SetField(template, "cargoOptions", new List<DeliveryCargoOption> { new DeliveryCargoOption { Item = _testItems[0], Weight = 1 } });
        MissionPool(template);
        var offer = new PlanetMissionOfferGenerator().TryGenerateOffer(_systems[0], _testPlanets[0], _missions.GetActiveMissions(), null);
        Assert.That(offer, Is.Not.Null);
        Assert.That(offer.OfferedMission.Objective.MissionCargoAmount, Is.EqualTo(3));
        Assert.That(offer.OfferedMission.Objective.MissionCargoVolume, Is.EqualTo(6));
        Assert.That(offer.OfferedMission.Reward.Credits, Is.EqualTo(offer.OfferedMission.Objective.DeliveryFuelCost * 50));
    }

    [Test]
    public void Reroll_ReplacesOnlyCurrentPlanetOfferAndRemovesItsOldAvailableCard()
    {
        var service = RerollService(() => Offer("new", "planet_A"));
        var old = Offer("old", "planet_A"); var other = Offer("other", "planet_B");
        _state.MissionBlock.OffersByPlanet["planet_A"] = old; _state.MissionBlock.OffersByPlanet["planet_B"] = other;
        _state.MissionBlock.OffersByPlanet_List.AddRange(new[] { old, other });
        _missions.SetAvailableMissions(new List<MissionInstanceData> { old.OfferedMission, other.OfferedMission });
        int saves = 0; _bus.Subscribe<SaveNeedEvent>(e => { saves++; Assert.That(_saving, Is.True); });
        Assert.That(service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out var fresh), Is.True);
        Assert.That(fresh.OfferedMission.MissionRuntimeId, Is.EqualTo("new"));
        Assert.That(_state.MissionBlock.OffersByPlanet["planet_B"], Is.SameAs(other));
        Assert.That(_missions.GetAvailableMissions().Select(m => m.MissionRuntimeId), Is.EqualTo(new[] { "other" }));
        Assert.That(_state.MissionBlock.OffersByPlanet_List.Select(o => o.OfferedMission.MissionRuntimeId), Is.EquivalentTo(new[] { "other", "new" }));
        Assert.That(saves, Is.EqualTo(1)); Assert.That(_saving, Is.True);
    }

    [Test]
    public void Reroll_NoEligibleMissionClearsStaleOfferInsteadOfReturningCachedMission()
    {
        var service = RerollService(() => null); var old = Offer("old", "planet_A");
        _state.MissionBlock.OffersByPlanet["planet_A"] = old;
        _missions.SetAvailableMissions(new List<MissionInstanceData> { old.OfferedMission });
        Assert.That(service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out var fresh), Is.True);
        Assert.That(fresh, Is.Null);
        Assert.That(_state.MissionBlock.OffersByPlanet.ContainsKey("planet_A"), Is.False);
        Assert.That(_missions.GetAvailableMissions(), Is.Empty);
    }

    [TestCase(true)] [TestCase(false)]
    public void Reroll_RejectsAcceptedMissionOrCapacityLimitWithoutChangingAnything(bool acceptedHere)
    {
        int calls = 0; var service = RerollService(() => { calls++; return Offer("new", "planet_A"); });
        var old = Offer("old", "planet_A"); _state.MissionBlock.OffersByPlanet["planet_A"] = old;
        _state.MissionBlock.ActiveMissions.Add(new MissionInstanceData { MissionRuntimeId = "active", Status = MissionStatus.InProgress,
            SourcePlanetId = acceptedHere ? "planet_A" : "planet_B" });
        if (!acceptedHere) Bootstrapper.Instance.MaxAcceptedMissionCount = 1;
        Assert.That(service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out _), Is.False);
        Assert.That(calls, Is.Zero);
        Assert.That(_state.MissionBlock.OffersByPlanet["planet_A"], Is.SameAs(old));
        Assert.That(_missions.GetActiveMissions().Count, Is.EqualTo(1));
    }

    [Test]
    public void Reroll_RejectsPlayerInSpaceAndKeepsExistingOffer()
    {
        var service = RerollService(() => throw new Exception("Must not generate in space"));
        var old = Offer("old", "planet_A"); _state.MissionBlock.OffersByPlanet["planet_A"] = old;
        _state.Player.CurrentPlanetId = "";
        Assert.That(service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out _), Is.False);
        Assert.That(_state.MissionBlock.OffersByPlanet["planet_A"], Is.SameAs(old));
    }

    [TestCase(true)] [TestCase(false)]
    public void Reroll_GeneratorFailurePreservesOfferAndOriginalSaveMode(bool saving)
    {
        var service = RerollService(() => throw new InvalidOperationException("Generator failure"));
        var old = Offer("old", "planet_A"); _state.MissionBlock.OffersByPlanet["planet_A"] = old; _saving = saving;
        Assert.Throws<InvalidOperationException>(() => service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out _));
        Assert.That(_saving, Is.EqualTo(saving));
        Assert.That(_state.MissionBlock.OffersByPlanet["planet_A"], Is.SameAs(old));
    }

    [Test]
    public void Reroll_DisabledSavingStaysDisabledAndDoesNotRequestSave()
    {
        var service = RerollService(() => Offer("new", "planet_A")); _saving = false;
        int saves = 0; _bus.Subscribe<SaveNeedEvent>(e => saves++);
        Assert.That(service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out _), Is.True);
        Assert.That(_saving, Is.False); Assert.That(saves, Is.Zero);
    }

    [Test]
    public void Reroll_CurrentEnabledTemplatesAreUsedInsteadOfSavedOffer()
    {
        var template = Config<RouteMissionTemplateConfig>("route_template"); SetBaseTemplate(template, 456); MissionPool(template);
        _registry.Register<IPlanetMissionOfferStateService>(new PlanetMissionOfferStateService());
        _registry.Register<IPlanetMissionOfferGenerator>(new PlanetMissionOfferGenerator());
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "planet_A";
        var old = Offer("old", "planet_A"); _state.MissionBlock.OffersByPlanet["planet_A"] = old;
        var service = new PlanetGovernmentMissionService();
        Assert.That(service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out var fresh), Is.True);
        Assert.That(fresh.OfferedMission.MissionType, Is.EqualTo(MissionType.RouteSearch));
        Assert.That(fresh.OfferedMission.Reward.Credits, Is.EqualTo(456));
        Assert.That(fresh.OfferedMission.MissionRuntimeId, Is.Not.EqualTo("old"));
    }

    [Test]
    public void Delivery_PaidRewardLedgerSurvivesFullSaveLoad()
    {
        var mission = DeliveryOffer(); Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Player.CurrentSystemId = "B"; _state.Player.CurrentPlanetId = "planet_B";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        ReloadState();
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_state.Player.Credits, Is.EqualTo(200));
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Does.Contain("delivery"));
    }

    [Test]
    public void EnemyDestroyedInOtherSystem_DoesNotAdvanceCurrentEncounterOrMission()
    {
        StartBattle(false);
        var runtime = new SystemNpcRuntimeService();
        var other = Enemy("remote", 5, "B"); runtime.AddNpc(other);
        Assert.That(runtime.KillNpc("remote", true), Is.True);
        Assert.That(_encounter.Current.EnemiesAlive, Is.EqualTo(2));
        Assert.That(_encounter.Current.PlayerKills, Is.Zero);
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
    }

    [Test]
    public void Reroll_MutationFailureRestoresOfferAvailableCardAndSaveMode()
    {
        var service = RerollService(() => Offer("new", "planet_A"), true);
        var old = Offer("old", "planet_A");
        _state.MissionBlock.OffersByPlanet["planet_A"] = old;
        _state.MissionBlock.OffersByPlanet_List.Add(old);
        _missions.SetAvailableMissions(new List<MissionInstanceData> { old.OfferedMission });
        int saves = 0; _bus.Subscribe<SaveNeedEvent>(e => saves++);
        Assert.Throws<InvalidOperationException>(() => service.TryRegenerateGovernmentOffer(_systems[0], _testPlanets[0], out _));
        Assert.That(_state.MissionBlock.OffersByPlanet["planet_A"], Is.SameAs(old));
        Assert.That(_state.MissionBlock.OffersByPlanet_List.Single(), Is.SameAs(old));
        Assert.That(_missions.GetAvailableMissions().Single(), Is.SameAs(old.OfferedMission));
        Assert.That(_saving, Is.True); Assert.That(saves, Is.Zero);
    }

    private MissionInstanceData DeliveryOffer()
    {
        PrepareCargo(20);
        var mission = new MissionInstanceData { MissionRuntimeId = "delivery", MissionType = MissionType.Delivery,
            Status = MissionStatus.Available, SourceSystemId = "A", SourcePlanetId = "planet_A", TargetSystemId = "B", TargetPlanetId = "planet_B",
            Reward = new MissionRewardData { Credits = 200 }, Objective = new MissionObjectiveData { TargetSystemId = "B", RequiredAmount = 5,
                MissionCargoItemId = "goods", MissionCargoAmount = 5, MissionCargoVolume = 10 } };
        _missions.SetAvailableMissions(new List<MissionInstanceData> { mission }); return mission;
    }
    private void PrepareCargo(int capacity)
    {
        if (_testItems.Count == 0) { var item = Config<ItemConfig>("goods"); SetField(item, "cargoSize", 2); _testItems.Add(item); }
        var ship = new ShipRuntimeData { ShipId = "ship", CargoCapacity = capacity };
        _state.Player.PlayerShipState.OwnedShips.Clear(); _state.Player.PlayerShipState.OwnedShips.Add(ship); _state.Player.PlayerShipState.ActiveShipId = "ship";
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "planet_A";
    }
    private MissionInstanceData ReconOffer()
    {
        _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "planet_A";
        var mission = new MissionInstanceData { MissionRuntimeId = "recon", MissionType = MissionType.Recon, Status = MissionStatus.Available,
            SourceSystemId = "A", SourcePlanetId = "planet_A", TargetSystemId = "B", Reward = new MissionRewardData { Credits = 150 },
            Objective = new MissionObjectiveData { TargetSystemId = "B", RequiredAmount = 2, ReconScanDistance = 40 } };
        _missions.SetAvailableMissions(new List<MissionInstanceData> { mission }); return mission;
    }
    private SystemNpcRuntimeState Enemy(string id, float x, string system = "B")
    {
        var enemy = new SystemNpcRuntimeState { RuntimeNpcId = id, NpcType = SystemNpcType.Enemy, CurrentSystemId = system,
            IsAlive = true, LifeState = SystemNpcLifeState.Alive, CurrentHull = 10, MaxHull = 10, CurrentPosition = new Vector3(x, 0, 0) };
        _testNpcs.Add(enemy); return enemy;
    }
    private void ReloadState()
    {
        _state = JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_state));
        MissionLifecycleRules.Normalize(_state.MissionBlock, 1);
        _missions.ReevaluateActiveMissions("AfterLoad");
    }
    private void SetBaseTemplate(MissionTemplateConfig template, int credits)
    {
        SetField(template, "titlePattern", "Mission {targetSystem}"); SetField(template, "descriptionPattern", "From {sourcePlanet} to {targetSystem}");
        SetField(template, "creditRewardMode", MissionCreditRewardMode.Fixed); SetField(template, "rewardCredits", credits);
    }
    private void MissionPool(MissionTemplateConfig template)
    {
        var pool = Config<PlanetMissionConfig>("pool"); var entry = Config<PlanetMissionOfferEntryConfig>("entry");
        SetField(entry, "missionTemplateConfig", template); SetField(entry, "isEnabled", true); SetField(entry, "weight", 1);
        pool.PossibleMissionOffers.Add(entry); SetField(_testPlanets[0], "planetMissionConfig", pool);
    }
    private PlanetGovernmentMissionService RerollService(Func<PlanetOfferedMissionData> generator, bool failOnNewSet = false)
    {
        SetField(_testPlanets[0], "planetMissionConfig", Config<PlanetMissionConfig>("pool"));
        _state.Player.CurrentSystemId = "A"; _state.Player.CurrentPlanetId = "planet_A";
        var offers = new PlanetMissionOfferStateService();
        if (!failOnNewSet) _registry.Register<IPlanetMissionOfferStateService>(offers);
        else _registry.Register(S08MissionTestProxy.Make<IPlanetMissionOfferStateService>((m, a) =>
        {
            if (m.Name == "GetOffer") return offers.GetOffer((string)a[0]);
            if (m.Name == "ClearOffer") { offers.ClearOffer((string)a[0]); return null; }
            if (m.Name == "SetOffer")
            {
                var incoming = (PlanetOfferedMissionData)a[0]; offers.SetOffer(incoming);
                if (failOnNewSet && incoming.OfferedMission.MissionRuntimeId == "new")
                { failOnNewSet = false; throw new InvalidOperationException("SetOffer failure"); }
                return null;
            }
            return Default(m);
        }));
        _registry.Register(S08MissionTestProxy.Make<IPlanetMissionOfferGenerator>((m, a) =>
        { Assert.That(a[3], Is.Null, "Reroll must bypass the cached offer."); return generator(); }));
        return new PlanetGovernmentMissionService();
    }
    private static PlanetOfferedMissionData Offer(string id, string planet) => new PlanetOfferedMissionData { PlanetId = planet,
        OfferedMission = new MissionInstanceData { MissionRuntimeId = id, SourcePlanetId = planet, MissionType = MissionType.Delivery,
            Status = MissionStatus.Available, Objective = new MissionObjectiveData { RequiredAmount = 1 }, Reward = new MissionRewardData() } };
}
