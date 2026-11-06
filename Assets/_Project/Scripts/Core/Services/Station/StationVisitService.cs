using System;
using UnityEngine;

public sealed class StationVisitService : IStationVisitService
{
    private readonly IGameSessionService _session;
    private readonly Func<string, StarSystemConfig> _resolve;
    private readonly IStationService _stations;
    private readonly ISystemTravelService _travel;
    private readonly IShipMovementService _movement;
    private readonly IPlayerControlService _control;
    private readonly SimpleEventBus _events;
    private readonly Func<IGameTimeService> _time;
    private readonly Func<bool> _departureBlocked;
    private string _insideSystem;
    private bool _ownsDisabled;
    private bool _controlEnabled;
    private bool _movementEnabled;
    public StationVisitService() : this(Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>().GetStarSystemConfigById,
        Bootstrapper.Instance.ServiceRegistry.Get<IStationService>(), Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IShipMovementService>(), Bootstrapper.Instance.ServiceRegistry.Get<IPlayerControlService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>(),
        () => Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>(), PlayerCargoOverload2A.RejectRouteIfOverloaded) { }
    public StationVisitService(IGameSessionService session, Func<string, StarSystemConfig> resolve, IStationService stations,
        ISystemTravelService travel, IShipMovementService movement, IPlayerControlService control, SimpleEventBus events,
        Func<IGameTimeService> time = null, Func<bool> departureBlocked = null)
    {
        _session = session; _resolve = resolve; _stations = stations; _travel = travel; _movement = movement; _control = control; _events = events;
        _time = time; _departureBlocked = departureBlocked;
        _events?.Subscribe<StationStateChangedEvent>(OnStationChanged);
        _events?.Subscribe<DestinationSelectedEvent>(OnDestination);
        _events?.Subscribe<TravelFinishedEvent>(OnTravelFinished);
        _events?.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
    }
    public void InitializeSession()
    {
        RestoreInput(); _insideSystem = null;
        var player = _session.State?.Player;
        if (player == null || string.IsNullOrEmpty(player.CurrentStationId)) return;
        string saved = player.CurrentStationId;
        _movement.SetPosition(player.SystemMapShipPosition); _travel.SetCurrentPosition(player.SystemMapShipPosition);
        var config = _resolve(player.CurrentSystemId)?.Station;
        if (config == null || config.Id != saved || !TryEnter(player.CurrentSystemId, out _)) player.CurrentStationId = string.Empty;
    }
    public bool TryEnter(string systemId, out string reason)
    {
        reason = "StationUnavailable";
        var player = _session.State?.Player;
        var config = _resolve(systemId)?.Station;
        if (player == null || player.GetActiveShip() == null || player.GetActiveShip().CurrentHull <= 0 || player.CurrentSystemId != systemId || config == null ||
            !_stations.TryGetStation(systemId, out var station) || !station.CanDock || station.ConfigMissing ||
            station.ConfigId != config.Id || _session.State.Galaxy.Systems.Find(x => x != null && x.SystemId == systemId)?.SystemStatus == StarSystemStatus.Captured) return false;
        if (!string.IsNullOrEmpty(player.CurrentPlanetId)) return false;
        if (Vector2.Distance(_movement.State.Position, config.LocalOffset) >= config.OperationInteractionDistance)
        { reason = "ApproachRequired"; return false; }
        if (_ownsDisabled && player.CurrentStationId == station.ConfigId) { reason = string.Empty; return true; }
        _controlEnabled = _control.IsEnabled; _movementEnabled = _movement.IsEnabled; _ownsDisabled = true;
        _movement.StopImmediately(); _control.SetEnabled(false); _movement.SetEnabled(false);
        _insideSystem = systemId; player.CurrentStationId = station.ConfigId; player.CurrentPlanetId = string.Empty;
        player.SystemMapShipPosition = _movement.State.Position;
        reason = string.Empty;
        _events?.PublishSafely(new StationVisitChangedEvent(systemId, station.ConfigId, true, false), _ => { });
        _events?.PublishSafely(new SaveNeedEvent("station_entered"), _ => { });
        Log("ENTERED", station.ConfigId); return true;
    }
    private void RestoreInput()
    {
        if (!_ownsDisabled) return;
        _control.SetEnabled(_controlEnabled); _movement.SetEnabled(_movementEnabled); _ownsDisabled = false;
    }
    public bool TryTakeOff(out string reason)
    {
        reason = "StationUnavailable";
        var player = _session.State?.Player;
        var system = player != null ? _resolve(player.CurrentSystemId) : null;
        if (!_ownsDisabled || !StationLocationRules.IsInside(_session.State, system)) return false;
        if (_departureBlocked?.Invoke() == true) { reason = "CargoOverloaded"; Log("TAKEOFF_BLOCKED", player.CurrentStationId); return false; }
        string systemId = player.CurrentSystemId;
        Vector2 position = _movement.State.Position;
        _travel.CancelTravel();
        _travel.SetCurrentPosition(position);
        player.SystemMapShipPosition = position;
        EndVisit(false, false, false, false);
        // Rebuild the system view before constructing the route, just as on a planet.
        _events?.PublishSafely(new StarSystemEnteredEvent(systemId), _ => { });
        if (!_travel.StartPlanetTakeoff())
        {
            _travel.CancelTravel();
            _movement.StopImmediately();
            _movement.SetPosition(position); _travel.SetCurrentPosition(position);
            player.SystemMapShipPosition = position;
            TryEnter(systemId, out _);
            reason = "TakeoffRouteUnavailable"; Log("TAKEOFF_FAILED", system.Station.Id); return false;
        }
        reason = string.Empty;
        var time = _time?.Invoke();
        if (time?.IsPaused == true) time.StepOneDay();
        _events?.PublishSafely(new SaveNeedEvent("station_takeoff"), _ => { });
        Log("TAKEOFF", system.Station.Id); return true;
    }
    public void Leave(bool forced = false) => EndVisit(forced, true);
    private void EndVisit(bool forced, bool cancelRoute, bool reposition = true, bool save = true)
    {
        var player = _session.State?.Player;
        if (player == null) { RestoreInput(); _insideSystem = null; return; }
        string id = player.CurrentStationId;
        if (string.IsNullOrEmpty(id)) { RestoreInput(); _insideSystem = null; return; }
        string system = _insideSystem ?? player.CurrentSystemId;
        player.CurrentStationId = string.Empty; RestoreInput(); _insideSystem = null;
        if (cancelRoute) _travel.CancelTravel();
        // Never alter hull/shield on leaving a station, including destruction.
        if (reposition && player.CurrentSystemId == system && string.IsNullOrEmpty(player.CurrentPlanetId))
        {
            var config = _resolve(system)?.Station;
            Vector2 exit = (config != null ? config.LocalOffset : _movement.State.Position) + Vector2.down * 60f;
            _movement.SetPosition(exit); _travel.SetCurrentPosition(exit); player.SystemMapShipPosition = exit;
        }
        _events?.PublishSafely(new StationVisitChangedEvent(system, id, false, forced), _ => { });
        if (save) _events?.PublishSafely(new SaveNeedEvent("station_left"), _ => { });
        Log(forced ? "EJECTED" : "LEFT", id);
    }
    public void Tick()
    {
        var player = _session.State?.Player;
        if (player == null) { RestoreInput(); return; }
        if (string.IsNullOrEmpty(player.CurrentStationId)) { RestoreInput(); _insideSystem = null; return; }
        var config = _resolve(player.CurrentSystemId)?.Station;
        if (player.GetActiveShip() == null || player.GetActiveShip().CurrentHull <= 0 || config == null || config.Id != player.CurrentStationId || !_stations.TryGetStation(player.CurrentSystemId, out var state) ||
            !state.CanDock || state.ConfigMissing ||
            _session.State.Galaxy.Systems.Find(x => x != null && x.SystemId == player.CurrentSystemId)?.SystemStatus == StarSystemStatus.Captured) Leave(true);
    }
    private void OnPlanetEntered(PlanetEnteredEvent evt) { if (_ownsDisabled) EndVisit(false, false); }
    private void OnStationChanged(StationStateChangedEvent evt) { Tick(); }
    private void OnDestination(DestinationSelectedEvent evt) { if (_ownsDisabled) EndVisit(false, false); }
    private void OnTravelFinished(TravelFinishedEvent evt) { if (evt.Success && evt.FromSystemId != evt.ToSystemId) EndVisit(false, false); }
    private static void Log(string action, string id) => AppLog.Info(DebugLogChannel.StationInterface, "[StationInterface] " + action + " | Station=" + id);
}
