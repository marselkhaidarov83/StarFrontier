using NUnit.Framework;
using UnityEngine;

public sealed class StationArrivalBuildDebugEditModeTests
{
    [TestCase(false)] [TestCase(true)]
    public void InstantBuild_ConstructsFirstLevelAndCannotUpgradeOrHealOnRepeat(bool alreadyStarted)
    {
        var system = new StarSystemRuntimeState { SystemId = "sys", Station = new StationRuntimeState { ConfigId = "station" } };
        if (alreadyStarted) system.Station.Development = new StationOperationRuntimeState { OperationId = "paid", Kind = StationOperationKind.Construction,
            TargetLevel = 1, DurationSeconds = 300, ElapsedSeconds = 10, PaidCosts = new[] { new StationPaymentCostSnapshot { Resource = StationPaymentResource.Credits, Amount = 500 } } };
        Assert.That(StationArrivalBuildDebug2A.TryBuildFirstLevel(system, 400, false, out _), Is.True);
        Assert.That(system.Station.Level, Is.EqualTo(1)); Assert.That(system.Station.Development, Is.Null);
        Assert.That(system.Station.Health, Is.EqualTo(400)); Assert.That(system.Station.MaxHealth, Is.EqualTo(400));
        system.Station.Health = 300;
        string before = JsonUtility.ToJson(system);
        Assert.That(StationArrivalBuildDebug2A.TryBuildFirstLevel(system, 400, false, out _), Is.False);
        Assert.That(JsonUtility.ToJson(system), Is.EqualTo(before));
    }
    [TestCase(StarSystemStatus.Captured, false)] [TestCase(StarSystemStatus.Stable, true)]
    public void InstantBuild_RejectsCapturedOrUnderAttackWithoutMutation(StarSystemStatus status, bool enemies)
    {
        var system = new StarSystemRuntimeState { SystemStatus = status, Station = new StationRuntimeState() };
        string before = JsonUtility.ToJson(system);
        Assert.That(StationArrivalBuildDebug2A.TryBuildFirstLevel(system, 400, enemies, out _), Is.False);
        Assert.That(JsonUtility.ToJson(system), Is.EqualTo(before));
    }
    [Test] public void Arrival_UsesDustHarborRouteEntryAndVisitsEmberWatch()
    {
        var route = UnityEditor.AssetDatabase.LoadAssetAtPath<RouteConfig>(
            "Assets/_Project/Content/Configs/Galaxy/Routes/route_dust_harbor_ember_watch_02.asset");
        Assert.That(route, Is.Not.Null);
        var state = new GameRuntimeState(); state.Player.CurrentSystemId = StationArrivalBuildDebug2A.DustHarbor;
        state.Player.Credits = 123; state.Player.Diamonds = 45;
        var destination = new StarSystemRuntimeState { SystemId = StationArrivalBuildDebug2A.EmberWatch }; state.Galaxy.Systems.Add(destination);
        Assert.That(StationArrivalBuildDebug2A.TryArrive(state, route, out _), Is.True);
        Assert.That(state.Player.CurrentSystemId, Is.EqualTo(StationArrivalBuildDebug2A.EmberWatch));
        Assert.That(state.Galaxy.CurrentSystemId, Is.EqualTo(state.Player.CurrentSystemId));
        Assert.That(state.Player.SystemMapShipPosition, Is.EqualTo(route.GetEntryPoint(StationArrivalBuildDebug2A.EmberWatch)));
        Assert.That(destination.IsDiscovered && destination.IsVisited, Is.True);
        Assert.That(state.Player.Credits, Is.EqualTo(123)); Assert.That(state.Player.Diamonds, Is.EqualTo(45));
    }

    [TestCase(false)] [TestCase(true)]
    public void Arrival_WorksFromOtherSystemsAndClearsDocking(bool station)
    {
        var route = UnityEditor.AssetDatabase.LoadAssetAtPath<RouteConfig>(
            "Assets/_Project/Content/Configs/Galaxy/Routes/route_dust_harbor_ember_watch_02.asset");
        Assert.That(route, Is.Not.Null);
        var state = new GameRuntimeState(); state.Player.CurrentSystemId = "other";
        state.Player.CurrentPlanetId = station ? string.Empty : "planet";
        state.Player.CurrentStationId = station ? "station" : string.Empty;
        state.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = StationArrivalBuildDebug2A.EmberWatch });
        Assert.That(StationArrivalBuildDebug2A.TryArrive(state, route, out _), Is.True);
        Assert.That(state.Player.CurrentSystemId, Is.EqualTo(StationArrivalBuildDebug2A.EmberWatch));
        Assert.That(state.Player.CurrentPlanetId, Is.Empty); Assert.That(state.Player.CurrentStationId, Is.Empty);
        Assert.That(state.Player.SystemMapShipPosition, Is.EqualTo(route.GetEntryPoint(StationArrivalBuildDebug2A.EmberWatch)));
    }
    [Test] public void Arrival_MissingRouteDoesNotChangePlayerLocationOrDocking()
    {
        var state = new GameRuntimeState(); state.Player.CurrentSystemId = "other"; state.Player.CurrentStationId = "station";
        string before = JsonUtility.ToJson(state);
        Assert.That(StationArrivalBuildDebug2A.TryArrive(state, null, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("RouteMissing")); Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
    }
}
