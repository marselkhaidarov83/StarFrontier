using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class S08_02_BattleAndRouteEditModeTests
{
    [Test]
    public void CapturedSourceFailsAcceptedDeliveryAndRemovesCargo()
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Captured;

        _missions.ReevaluateActiveMissions("SourceCaptured");

        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_missions.GetArchivedMissions().Single().Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(mission.LastTransitionReason, Is.EqualTo("SystemCaptured"));
        Assert.That(_state.Player.GetActiveShip().Cargo.entries, Is.Empty);
    }

    [Test]
    public void CapturedSourceFailsAcceptedRecon()
    {
        var mission = ReconOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Captured;

        _missions.ReevaluateActiveMissions("SourceCaptured");

        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_missions.GetArchivedMissions().Single().Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(mission.LastTransitionReason, Is.EqualTo("SystemCaptured"));
    }

    [Test]
    public void CapturedSourceFailsAcceptedRouteUpgrade()
    {
        _state.Galaxy.Routes.Find(r => r.RouteId == "AB").IsUnlocked = true;
        var mission = AcceptRoute(true);
        _state.Galaxy.Systems[0].SystemStatus = StarSystemStatus.Captured;

        _missions.ReevaluateActiveMissions("SourceCaptured");

        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_missions.GetArchivedMissions().Single().Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(mission.LastTransitionReason, Is.EqualTo("SystemCaptured"));
    }

    [Test]
    public void DeliveryTurnIn_FailsIfDestinationWasCapturedWithoutAStatusEvent()
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Player.CurrentSystemId = "B";
        _state.Player.CurrentPlanetId = "planet_B";
        _missions.ReevaluateActiveMissions("AtDestination");
        Assert.That(mission.Status, Is.EqualTo(MissionStatus.ReadyToComplete));

        _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_missions.GetArchivedMissions().Single().LastTransitionReason, Is.EqualTo("SystemCaptured"));
        Assert.That(_state.Player.Credits, Is.Zero);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void DeliveryFailure_RemainsArchivedAfterAnotherSaveLoad(bool destinationCaptured)
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        if (destinationCaptured)
            _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        else
            _state.Player.GetActiveShip().Cargo.entries.Clear();

        _missions.ReevaluateActiveMissions("FailureDetected");
        string expectedReason = destinationCaptured ? "SystemCaptured" : "MissionCargoLost";
        Assert.That(_missions.GetArchivedMissions().Single().LastTransitionReason, Is.EqualTo(expectedReason));
        if (destinationCaptured) _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Stable;
        ReloadState();

        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_missions.GetArchivedMissions().Single().LastTransitionReason, Is.EqualTo(expectedReason));
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [Test]
    public void DeliverySuccess_PaysConfiguredRewardWithoutChangingGalaxyState()
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Player.CurrentSystemId = "B";
        _state.Player.CurrentPlanetId = "planet_B";
        string galaxyBefore = JsonUtility.ToJson(_state.Galaxy);

        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Player.Credits, Is.EqualTo(200));
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.EquivalentTo(new[] { mission.MissionRuntimeId }));
        Assert.That(JsonUtility.ToJson(_state.Galaxy), Is.EqualTo(galaxyBefore));
    }

    [Test]
    public void RouteUpgrade_AfterPaymentAndReloadCannotPayOrAdvanceAgain()
    {
        _state.Galaxy.Routes.Find(r => r.RouteId == "AB").IsUnlocked = true;
        var mission = AcceptRoute(true);
        Hop("A", "B"); Hop("B", "A");
        Hop("A", "C"); Hop("C", "B"); Hop("B", "A");
        _state.Player.CurrentPlanetId = "issuer";
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Galaxy.Routes.Find(r => r.RouteId == "AB").DevelopmentLevel, Is.EqualTo(2));
        Assert.That(_state.Player.Credits, Is.EqualTo(150));

        ReloadState();
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_state.Galaxy.Routes.Find(r => r.RouteId == "AB").DevelopmentLevel, Is.EqualTo(2));
        Assert.That(_state.Player.Credits, Is.EqualTo(150));
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.EquivalentTo(new[] { mission.MissionRuntimeId }));
    }

    [Test]
    public void DeliveryGenerator_FormatsOutcomeTextAndKeepsItAfterSaveLoad()
    {
        PrepareCargo(30);
        foreach (var planet in _testPlanets)
            SetField(planet, "displayName", "Планета " + planet.Id);
        foreach (var system in _systems)
            SetField(system, "displayName", "Система " + system.Id);
        var template = Config<DeliveryMissionTemplateConfig>("delivery_text_template");
        SetBaseTemplate(template, 0);
        SetField(template, "creditRewardMode", MissionCreditRewardMode.FuelDistance);
        SetField(template, "fuelRewardDeflator", 50f);
        SetField(template, "minimumQuantity", 3);
        SetField(template, "maximumQuantity", 3);
        SetField(template, "cargoOptions", new List<DeliveryCargoOption>
        { new DeliveryCargoOption { Item = _testItems[0], Weight = 1 } });
        SetField(template, "outcomeTexts", new MissionOutcomeTexts
        {
            ReadyToTurnIn = "Сдайте груз на {targetPlanet}.",
            MissionCargoLost = "Груз для {targetPlanet} потерян.",
            SystemCaptured = "Система {targetSystem} захвачена."
        });
        MissionPool(template);

        var offer = new PlanetMissionOfferGenerator().TryGenerateOffer(
            _systems[0], _testPlanets[0], _missions.GetActiveMissions(), null);
        Assert.That(offer, Is.Not.Null);
        var generated = offer.OfferedMission;
        Assert.That(generated.OutcomeTexts, Is.Not.Null);
        Assert.That(generated.OutcomeTexts.ReadyToTurnIn, Does.Contain(_testPlanets.Single(p => p.Id == generated.TargetPlanetId).DisplayName));
        Assert.That(generated.OutcomeTexts.SystemCaptured, Does.Not.Contain("{"));
        var loaded = MissionSaveMapper.ToRuntimeData(JsonUtility.FromJson<MissionSaveData>(
            JsonUtility.ToJson(MissionSaveMapper.ToSaveData(generated))));
        Assert.That(loaded.OutcomeTexts.SystemCaptured, Is.EqualTo(generated.OutcomeTexts.SystemCaptured));
        Assert.That(loaded.Reward.Credits, Is.EqualTo(generated.Reward.Credits));
    }

    [TestCase(false)] [TestCase(true)]
    public void BattleMission_UsesConfiguredTextsForBothKinds(bool liberation)
    {
        SetField(_systems[0], "displayName", "Система A");
        var config = _registry.Get<IConfigService>().GameConfig.MissionGameplay;
        string prefix = liberation ? "liberation" : "defense";
        SetField(config, prefix + "TitlePattern", "Приказ: {targetSystem}");
        SetField(config, prefix + "DescriptionPattern", "Удержите систему {targetSystem}.");
        SetField(config, prefix + "OutcomeTexts", new MissionOutcomeTexts
        { ReadyToTurnIn = "Получите награду в системе {targetSystem}." });
        StartBattle(liberation);
        _missions.ObserveBattleParticipation("A");

        var mission = _missions.GetActiveMissions().Single();
        Assert.That(mission.MissionType, Is.EqualTo(liberation ? MissionType.Liberation : MissionType.Defense));
        Assert.That(mission.Title, Is.EqualTo("Приказ: " + _systems[0].DisplayName));
        Assert.That(mission.Description, Is.EqualTo("Удержите систему " + _systems[0].DisplayName + "."));
        Assert.That(mission.OutcomeTexts.ReadyToTurnIn, Is.EqualTo("Получите награду в системе " + _systems[0].DisplayName + "."));
    }

    [TestCase(false)] [TestCase(true)]
    public void DeliveryCapture_FailsImmediatelyAndCannotBeClaimedAfterLiberation(bool useSetStatus)
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Player.CurrentSystemId = "B";
        _state.Player.CurrentPlanetId = "planet_B";
        _missions.ReevaluateActiveMissions("AtDestination");
        Assert.That(mission.Status, Is.EqualTo(MissionStatus.ReadyToComplete));

        var security = new SystemSecurityService();
        Assert.That(useSetStatus ? security.SetSystemStatus("B", StarSystemStatus.Captured) :
            security.CaptureSystem("B"), Is.True);
        Assert.That(mission.Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(mission.LastTransitionReason, Is.EqualTo("SystemCaptured"));
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_state.Player.Credits, Is.Zero);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
        Assert.That(_state.Player.GetActiveShip().Cargo.entries, Is.Empty);

        _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Stable;
        _tracker.ReevaluateAfterLoad();
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_missions.GetArchivedMissions().Single().Status, Is.EqualTo(MissionStatus.Failed));
    }

    [Test]
    public void DeliveryCaptureAfterLoad_FailsBeforeTurnInAndPaysNothing()
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Galaxy.Systems[1].SystemStatus = StarSystemStatus.Captured;
        ReloadState();
        Assert.That(_missions.GetActiveMissions(), Is.Empty);
        Assert.That(_missions.GetArchivedMissions().Single().LastTransitionReason, Is.EqualTo("SystemCaptured"));
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [Test]
    public void DeliveryDirectTurnIn_RejectsCargoLossWithoutPayment()
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _state.Player.CurrentSystemId = "B";
        _state.Player.CurrentPlanetId = "planet_B";
        _missions.ReevaluateActiveMissions("AtDestination");
        _state.Player.GetActiveShip().Cargo.entries.Clear();

        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_missions.GetArchivedMissions().Single().LastTransitionReason, Is.EqualTo("MissionCargoLost"));
        Assert.That(_state.Player.Credits, Is.Zero);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
    }

    [Test]
    public void CargoChangeEvent_FailsDeliveryWhenMissionCargoIsRemoved()
    {
        var mission = DeliveryOffer();
        Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
        _registry.Get<IInventoryService>().Clear();
        Assert.That(_missions.GetArchivedMissions().Single().LastTransitionReason, Is.EqualTo("MissionCargoLost"));
        Assert.That(_state.Player.Credits, Is.Zero);
    }

    [Test]
    public void MissionOutcomeTexts_SurviveSaveMappingAndPickSpecificFailure()
    {
        var mission = DeliveryOffer();
        mission.OutcomeTexts = new MissionOutcomeTexts
        {
            ReadyToTurnIn = "Сдайте груз на планете Б.",
            Failed = "Доставка провалена.",
            MissionCargoLost = "Груз потерян.",
            SystemCaptured = "Планета недоступна из-за захвата."
        };
        var save = MissionSaveMapper.ToSaveData(mission);
        var loaded = MissionSaveMapper.ToRuntimeData(JsonUtility.FromJson<MissionSaveData>(JsonUtility.ToJson(save)));
        Assert.That(loaded.OutcomeTexts.For(MissionStatus.ReadyToComplete, "Ready"), Is.EqualTo("Сдайте груз на планете Б."));
        Assert.That(loaded.OutcomeTexts.For(MissionStatus.Failed, "MissionCargoLost"), Is.EqualTo("Груз потерян."));
        Assert.That(loaded.OutcomeTexts.For(MissionStatus.Failed, "SystemCaptured"), Is.EqualTo("Планета недоступна из-за захвата."));
        Assert.That(loaded.Reward.Credits, Is.EqualTo(200));
    }
}
