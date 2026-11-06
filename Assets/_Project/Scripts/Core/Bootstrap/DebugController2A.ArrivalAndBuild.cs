using System;
using System.Linq;
using UnityEngine;

public sealed partial class DebugController2A
{
    [ContextMenu("STAR FRONTIER/Travel/Прилететь в Угольный дозор из Пыльной гавани")]
    private void DebugArriveAtEmberWatch()
    {
        if (!CanRunArrivalAndBuildDebug()) return;
        var registry = ServiceRegistry;
        if (registry == null || !registry.TryGet<IGameSessionService>(out var session) ||
            !registry.TryGet<IConfigService>(out var config) || !registry.TryGet<ISystemTravelService>(out var navigation) ||
            !registry.TryGet<SimpleEventBus>(out var events) || !registry.TryGet<IGameStateMachine>(out var machine))
        { LogArrivalAndBuildRejection("ServicesUnavailable"); return; }
        var route = config.GetStarSystemConfigById(StationArrivalBuildDebug2A.DustHarbor)?.Routes?.FirstOrDefault(x => x != null && x.ConnectsSystems(StationArrivalBuildDebug2A.DustHarbor, StationArrivalBuildDebug2A.EmberWatch));
        if (!StationArrivalBuildDebug2A.CanArrive(session.State, route, out string reason))
        { LogArrivalAndBuildRejection(reason); return; }
        if (!string.IsNullOrEmpty(session.State.Player.CurrentStationId))
        {
            if (!registry.TryGet<IStationVisitService>(out var visit))
            { LogArrivalAndBuildRejection("StationVisitServiceUnavailable"); return; }
            visit.Leave();
        }
        if (!StationArrivalBuildDebug2A.TryArrive(session.State, route, out reason))
        { LogArrivalAndBuildRejection(reason); return; }
        Vector3 arrivalPosition = session.State.Player.SystemMapShipPosition;
        navigation.CancelTravel();
        navigation.State.CurrentPlanetId = string.Empty;
        navigation.SetCurrentSystem(StationArrivalBuildDebug2A.EmberWatch);
        navigation.SetCurrentPosition(arrivalPosition);
        session.State.Player.SystemMapShipPosition = arrivalPosition;
        events.PublishSafely(new TravelFinishedEvent(StationArrivalBuildDebug2A.DustHarbor, StationArrivalBuildDebug2A.EmberWatch,
            true, 0, TravelFailReason.None), e => LogArrivalAndBuildRejection("ArrivalObserver: " + e.Message));
        events.PublishSafely(new StarSystemEnteredEvent(StationArrivalBuildDebug2A.EmberWatch), e => LogArrivalAndBuildRejection(e.Message));
        events.PublishSafely(new CurrentSystemEnteredEvent(StationArrivalBuildDebug2A.EmberWatch), e => LogArrivalAndBuildRejection(e.Message));
        events.PublishSafely(new SaveNeedEvent("debug_arrival_ember_watch"), e => LogArrivalAndBuildRejection(e.Message));
        AppLog.Info(DebugLogChannel.DebugTools, "[ArrivalBuildDebug] ARRIVED | From=" + StationArrivalBuildDebug2A.DustHarbor +
            " | To=" + StationArrivalBuildDebug2A.EmberWatch + " | FuelSpent=0");
        machine.Enter(new SystemState());
    }

    [ContextMenu("STAR FRONTIER/Station/Мгновенно построить станцию в текущей системе")]
    private void DebugBuildCurrentStationInstantly()
    {
        if (!CanRunArrivalAndBuildDebug()) return;
        var registry = ServiceRegistry;
        if (registry == null || !registry.TryGet<IGameSessionService>(out var session) ||
            !registry.TryGet<IConfigService>(out var config) || !registry.TryGet<IStationService>(out var stations) ||
            !registry.TryGet<SimpleEventBus>(out var events))
        { LogArrivalAndBuildRejection("ServicesUnavailable"); return; }
        string systemId = session.State?.Player?.CurrentSystemId;
        var stationConfig = config.GetStarSystemConfigById(systemId)?.Station;
        if (stationConfig == null || !stations.TryGetStation(systemId, out _))
        { LogArrivalAndBuildRejection("StationUnavailable"); return; }
        var system = session.State.Galaxy.Systems.Find(x => x != null && x.SystemId == systemId);
        bool hasEnemies = registry.TryGet<ISystemNpcRuntimeService>(out var npcs) &&
            npcs.GetAliveNpcsInSystem(systemId).Any(x => x != null && x.IsAlive && (x.IsEnemy || x.IsHostileToPlayer));
        int health = stationConfig.LevelEffects?.GetLevel(1)?.EffectiveMaxHealth ?? StationInfrastructureRules.DefaultMaxHealth(1);
        if (!StationArrivalBuildDebug2A.TryBuildFirstLevel(system, health, hasEnemies, out string reason))
        { LogArrivalAndBuildRejection(reason); return; }
        stations.ReconcileSystem(systemId);
        events.PublishSafely(new SaveNeedEvent("debug_station_built"), e => LogArrivalAndBuildRejection(e.Message));
        AppLog.Info(DebugLogChannel.DebugTools, "[ArrivalBuildDebug] BUILT | System=" + systemId + " | Level=1");
    }
    private static bool CanRunArrivalAndBuildDebug()
    {
        bool allowed = DeveloperModeGate.CanRunExplicitCommand(Application.isPlaying, Application.isEditor, DeveloperModeGate.IsEnabled);
        if (!allowed) LogArrivalAndBuildRejection("PlayModeOrDeveloperModeRequired");
        return allowed;
    }
    private static void LogArrivalAndBuildRejection(string reason) =>
        AppLog.Warning(DebugLogChannel.DebugTools, "[ArrivalBuildDebug] Rejected | Reason=" + reason);
}

public static class StationArrivalBuildDebug2A
{
    public const string DustHarbor = "s02_system_dust_harbor_02";
    public const string EmberWatch = "s02_system_ember_watch_02";

    public static bool CanArrive(GameRuntimeState state, RouteConfig route, out string reason)
    {
        reason = "SessionUnavailable";
        if (state?.Player == null || state.Galaxy?.Systems == null) return false;
        if (route == null || !route.ConnectsSystems(DustHarbor, EmberWatch)) { reason = "RouteMissing"; return false; }
        if (!state.Galaxy.Systems.Any(x => x != null && x.SystemId == EmberWatch))
        { reason = "DestinationMissing"; return false; }
        reason = string.Empty; return true;
    }
    public static bool TryArrive(GameRuntimeState state, RouteConfig route, out string reason)
    {
        if (!CanArrive(state, route, out reason)) return false;
        var player = state.Player;
        var destination = state.Galaxy.Systems.Find(x => x != null && x.SystemId == EmberWatch);
        player.CurrentPlanetId = string.Empty; player.CurrentStationId = string.Empty;
        player.CurrentSystemId = EmberWatch; state.Galaxy.CurrentSystemId = EmberWatch;
        player.SystemMapShipPosition = route.GetEntryPoint(EmberWatch);
        destination.IsDiscovered = true; destination.IsVisited = true;
        reason = string.Empty; return true;
    }
    public static bool TryBuildFirstLevel(StarSystemRuntimeState system, int maxHealth, bool hasEnemies, out string reason)
    {
        reason = "StationUnavailable";
        var station = system?.Station;
        if (station == null) return false;
        if (system.SystemStatus == StarSystemStatus.Captured) { reason = "SystemCaptured"; return false; }
        if (hasEnemies || system.SystemStatus == StarSystemStatus.Invasion) { reason = "StationUnderAttack"; return false; }
        if (station.Level != 0) { reason = "AlreadyBuilt"; return false; }
        if (station.IsDestroyed || station.ConfigMissing || !station.IsEnabled || StationLifecycleRules.HasRecoveryOperation(station)) return false;
        var operation = station.Development;
        if (operation != null && (operation.Kind != StationOperationKind.Construction || operation.TargetLevel != 1))
        { reason = "InvalidOperation"; return false; }
        if (operation == null)
        {
            operation = new StationOperationRuntimeState { OperationId = Guid.NewGuid().ToString("N"),
                Kind = StationOperationKind.Construction, TargetLevel = 1, PaymentOptionId = "debug_free_construction" };
            if (!StationLifecycleRules.BeginPaidOperation(station, operation)) return false;
        }
        station.Development.ElapsedSeconds = station.Development.DurationSeconds;
        StationLifecycleRules.Advance(system, 0, _ => maxHealth, false);
        reason = station.Level == 1 ? string.Empty : "ConstructionNotCompleted";
        return station.Level == 1;
    }
}
