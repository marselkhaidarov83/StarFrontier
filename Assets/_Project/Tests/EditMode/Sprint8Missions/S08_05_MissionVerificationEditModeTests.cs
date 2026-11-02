using NUnit.Framework;

public sealed partial class S08_02_BattleAndRouteEditModeTests
{
    [TestCase(MissionType.Delivery)]
    [TestCase(MissionType.Recon)]
    [TestCase(MissionType.Defense)]
    [TestCase(MissionType.Liberation)]
    [TestCase(MissionType.RouteSearch)]
    public void EachPlayableType_RepeatedEvaluationAndReloadCannotPayTwice(MissionType type)
    {
        MissionInstanceData mission = CreateVerificationMission(type);
        int expectedCredits = mission.Reward.Credits;
        Assert.That(mission.Status, Is.EqualTo(type == MissionType.Defense ||
            type == MissionType.Liberation ? MissionStatus.ReadyToComplete : MissionStatus.InProgress));
        if (type == MissionType.Delivery || type == MissionType.Recon || type == MissionType.RouteSearch)
        {
            int steps = mission.Objective.RequiredAmount;
            for (int i = 0; i < steps; i++)
                Assert.That(_missions.TryDebugAdvanceMission(mission.MissionRuntimeId, out string reason),
                    Is.True, reason);
            Assert.That(_missions.TryDebugAdvanceMission(mission.MissionRuntimeId, out _), Is.False);
        }

        if (type == MissionType.Delivery)
        {
            _state.Player.CurrentSystemId = "B";
            _state.Player.CurrentPlanetId = "planet_B";
        }
        _missions.ReevaluateActiveMissions("DuplicateEvent");
        _missions.ReevaluateActiveMissions("DuplicateEvent");
        Assert.That(mission.Status, Is.EqualTo(MissionStatus.ReadyToComplete));
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.True);
        Assert.That(_state.Player.Credits, Is.EqualTo(expectedCredits));
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds,
            Is.EquivalentTo(new[] { mission.MissionRuntimeId }));

        ReloadState();
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_state.Player.Credits, Is.EqualTo(expectedCredits));
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds,
            Is.EquivalentTo(new[] { mission.MissionRuntimeId }));
    }

    [TestCase(MissionType.Delivery)]
    [TestCase(MissionType.Recon)]
    [TestCase(MissionType.Defense)]
    [TestCase(MissionType.Liberation)]
    [TestCase(MissionType.RouteSearch)]
    public void EachPlayableType_FailureIsTerminalAndCannotPay(MissionType type)
    {
        MissionInstanceData mission = CreateVerificationMission(type);
        Assert.That(_missions.FailMission(mission.MissionRuntimeId), Is.True);
        Assert.That(mission.Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(_missions.FailMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        ReloadState();
        Assert.That(_missions.GetArchivedMissions()[0].Status, Is.EqualTo(MissionStatus.Failed));
        Assert.That(_state.Player.Credits, Is.Zero);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DebugBattleProgress_DuplicateEvaluationDoesNotGrantReward(bool liberation)
    {
        StartBattle(liberation);
        _missions.ObserveBattleParticipation("A");
        var mission = _missions.GetActiveMissions()[0];
        Assert.That(_missions.TryDebugAdvanceMission(mission.MissionRuntimeId, out string reason),
            Is.True, reason);
        _missions.ReevaluateActiveMissions("DuplicateEvent");
        _missions.ReevaluateActiveMissions("DuplicateEvent");
        Assert.That(mission.Objective.CurrentAmount, Is.EqualTo(1));
        Assert.That(mission.Status, Is.EqualTo(MissionStatus.InProgress));
        Assert.That(_missions.CompleteMission(mission.MissionRuntimeId), Is.False);
        Assert.That(_state.Player.Credits, Is.Zero);
        Assert.That(_state.MissionBlock.RewardGrantedMissionIds, Is.Empty);
    }

    private MissionInstanceData CreateVerificationMission(MissionType type)
    {
        switch (type)
        {
            case MissionType.Delivery:
            {
                var mission = DeliveryOffer();
                Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
                return mission;
            }
            case MissionType.Recon:
            {
                var mission = ReconOffer();
                Assert.That(_missions.AcceptMission(mission.MissionRuntimeId), Is.True);
                return mission;
            }
            case MissionType.RouteSearch:
                return AcceptRoute(false);
            case MissionType.Defense:
            case MissionType.Liberation:
            {
                _state.Player.CurrentPlanetId = "planet_A";
                Assert.That(_missions.TryCreateDebugReadyBattleMission(type, 1,
                    out var mission, out string reason), Is.True, reason);
                return mission;
            }
            default:
                Assert.Fail("Unsupported mission type: " + type);
                return null;
        }
    }
}
