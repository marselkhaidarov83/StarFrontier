using NUnit.Framework;

public sealed class InvasionCaptureConditionsEditModeTests
{
    private static SystemNpcRuntimeState Npc(SystemNpcType type, bool alive = true)
    {
        return new SystemNpcRuntimeState
        {
            CurrentSystemId = "target", NpcType = type, IsAlive = alive
        };
    }

    [TestCase("target", true, false)]
    [TestCase("target", false, false)]
    [TestCase("other", true, false)]
    [TestCase("other", false, true)]
    public void Capture_RequiresNoDefendersAndPlayerOutside(
        string playerSystemId, bool defenderAlive, bool expected)
    {
        var npcs = new[] { Npc(SystemNpcType.Ally, defenderAlive), Npc(SystemNpcType.Enemy) };
        Assert.That(InvasionCaptureRules2A.CanCapture("target", playerSystemId, npcs), Is.EqualTo(expected));
    }

    [TestCase(true, SystemNpcTravelState.OnPlanet)]
    [TestCase(true, SystemNpcTravelState.Idle)]
    [TestCase(false, SystemNpcTravelState.OnPlanet)]
    public void AliveAllyOnPlanetDoesNotPreventCapture(bool isOnPlanet, SystemNpcTravelState travelState)
    {
        var defender = Npc(SystemNpcType.Ally);
        defender.IsOnPlanet = isOnPlanet;
        defender.TravelState = travelState;
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "other",
            new[] { defender, Npc(SystemNpcType.Enemy) }), Is.True);
    }

    [TestCase(SystemNpcTravelState.Idle)]
    [TestCase(SystemNpcTravelState.TravelingInsideSystem)]
    [TestCase(SystemNpcTravelState.TravelingToAnotherSystem)]
    [TestCase(SystemNpcTravelState.Patrolling)]
    [TestCase(SystemNpcTravelState.EngagingEnemy)]
    public void AliveAllyInSpacePreventsCapture(SystemNpcTravelState travelState)
    {
        var defender = Npc(SystemNpcType.Ally);
        defender.TravelState = travelState;
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "other",
            new[] { defender, Npc(SystemNpcType.Enemy) }), Is.False);
    }

    [TestCase(true, SystemNpcTravelState.OnPlanet)]
    [TestCase(true, SystemNpcTravelState.Idle)]
    [TestCase(false, SystemNpcTravelState.OnPlanet)]
    [TestCase(false, SystemNpcTravelState.EngagingEnemy)]
    public void AliveMilitaryPreventsCaptureOnPlanetsAndInSpace(
        bool isOnPlanet, SystemNpcTravelState travelState)
    {
        var defender = Npc(SystemNpcType.Ally);
        defender.AllyRole = AllyRole2A.Military;
        defender.IsOnPlanet = isOnPlanet;
        defender.TravelState = travelState;
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "other",
            new[] { defender, Npc(SystemNpcType.Enemy) }), Is.False);
    }

    [Test]
    public void DeadMilitaryAndLivingCivilianOnPlanetAllowCapture()
    {
        var military = Npc(SystemNpcType.Ally, false);
        military.AllyRole = AllyRole2A.Military;
        military.IsOnPlanet = true;
        var civilian = Npc(SystemNpcType.Ally);
        civilian.AllyRole = AllyRole2A.Trader;
        civilian.IsOnPlanet = true;
        civilian.TravelState = SystemNpcTravelState.OnPlanet;
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "other",
            new[] { military, civilian, Npc(SystemNpcType.Enemy) }), Is.True);
    }

    [TestCase(15, 15, 5, false)]
    [TestCase(15, 19, 5, false)]
    [TestCase(15, 20, 5, true)]
    [TestCase(15, 21, 5, true)]
    [TestCase(15, 15, 0, true)]
    [TestCase(15, 14, 5, false)]
    public void CaptureDelayUsesElapsedGameTicks(
        int startedAtTick, int currentTick, int minimumTicks, bool expected)
    {
        Assert.That(InvasionCaptureRules2A.HasMinimumDurationElapsed(
            startedAtTick, currentTick, minimumTicks), Is.EqualTo(expected));
    }

    [Test]
    public void PlayerInSystemPreventsCaptureEvenWhenAllAlliesAreOnPlanets()
    {
        var defender = Npc(SystemNpcType.Ally);
        defender.IsOnPlanet = true;
        defender.TravelState = SystemNpcTravelState.OnPlanet;
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "target",
            new[] { defender, Npc(SystemNpcType.Enemy) }), Is.False);
    }

    [Test]
    public void NoSurvivingInvadersCannotCaptureAnEmptySystem()
    {
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "other",
            new[] { Npc(SystemNpcType.Ally, false), Npc(SystemNpcType.Enemy, false) }), Is.False);
    }

    [Test]
    public void DefendersInAnotherSystemDoNotPreventCapture()
    {
        var defender = Npc(SystemNpcType.Ally);
        defender.CurrentSystemId = "elsewhere";
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "other",
            new[] { defender, Npc(SystemNpcType.Enemy) }), Is.True);
    }

    [Test]
    public void MissingRuntimeStateDoesNotAuthorizeCapture()
    {
        Assert.That(InvasionCaptureRules2A.CanCapture("target", "other", null), Is.False);
        Assert.That(InvasionCaptureRules2A.CanCapture("target", null,
            new[] { Npc(SystemNpcType.Enemy) }), Is.False);
    }

    [Test]
    public void OfflineElapsedTimeDoesNotCaptureOrGiveOwnership()
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "target", SystemStatus = StarSystemStatus.Invasion });
        galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "safe", SystemStatus = StarSystemStatus.Stable });
        galaxy.Invasions.Add(new InvasionState
        {
            InvasionId = "attack", FactionId = "ai", TargetSystemId = "target",
            LifecycleState = InvasionLifecycleState.Active, ResolveAtTick = 1
        });
        galaxy.ProcessOfflineWarCatchUp(100);
        Assert.That(galaxy.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
        Assert.That(galaxy.Invasions[0].IsActive(), Is.True);
        Assert.That(galaxy.EnemyFactions, Is.Empty);
    }

    [Test]
    public void OrphanInvasionStatusCannotBecomeCapturedFromTimeAlone()
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "target", SystemStatus = StarSystemStatus.Invasion });
        galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "safe", SystemStatus = StarSystemStatus.Stable });
        galaxy.ProcessOfflineWarCatchUp(100);
        Assert.That(galaxy.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
    }
}
