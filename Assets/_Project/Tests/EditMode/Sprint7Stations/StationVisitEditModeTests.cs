using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class StationVisitEditModeTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
    private Session _session; private Movement _movement; private Control _control; private Travel _travel;
    private Clock _time; private bool _departureBlocked;
    private SimpleEventBus _events; private StationService _stations; private StationVisitService _visit;
    private StarSystemConfig _systemConfig; private StationConfig _station;
    private StarSystemRuntimeState System => _session.State.Galaxy.Systems[0];
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Movement : IShipMovementService
    {
        public bool IsEnabled { get; private set; } = true;
        public ShipMovementRuntimeState State { get; } = new ShipMovementRuntimeState();
        public void SetEnabled(bool enabled) { IsEnabled = enabled; }
        public void SetShipStats(ShipFinalStats stats) { }
        public void ClearShipStats() { }
        public void SetPosition(Vector2 position) { State.SetPosition(position); }
        public void SetFacingDirection(Vector2 direction) { State.SetFacingDirection(direction); }
        public void InitializeFromPlayerState(PlayerState player) { SetPosition(player.SystemMapShipPosition); }
        public void WriteToPlayerState(PlayerState player) { player.SystemMapShipPosition = State.Position; }
        public void StopImmediately() { State.SetVelocity(Vector2.zero, Vector2.zero); }
        public void ResetAll() { }
        public void Tick(float seconds) { }
    }
    private sealed class Control : IPlayerControlService
    {
        public bool IsEnabled { get; private set; } = true;
        public Vector2 RawMoveInput => Vector2.zero;
        public Vector2 ProcessedMoveInput => Vector2.zero;
        public void SetEnabled(bool enabled) { IsEnabled = enabled; }
        public void SetRawMoveInput(Vector2 input) { }
        public void PressInteract() { }
        public void ReleaseInteract() { }
        public void PressRecenterCamera() { }
        public void ResetAll() { }
        public void Tick(float seconds) { }
    }
    private sealed class Travel : ISystemTravelService
    {
        public SystemTravelState State { get; } = new SystemTravelState();
        public int CancelCalls, TakeoffCalls;
        public bool TakeoffAvailable = true;
        public Action BeforeTakeoff;
        public float CurrentEffectiveTravelSpeed => 0;
        public int NpcFollowModeIndex => 0;
        public int NpcFollowModeNumber => 1;
        public void SetCurrentSystem(string id) { State.CurrentSystemId = id; }
        public void SetCurrentPlanet(string id, Vector3 position) { State.CurrentPlanetId = id; State.SetCurrentPosition(position); }
        public void SetCurrentPosition(Vector3 position) { State.SetCurrentPosition(position); }
        public bool StartPlanetTakeoff() { TakeoffCalls++; BeforeTakeoff?.Invoke(); return TakeoffAvailable; }
        public void SetPlanetDestination(PlanetConfig config) { }
        public void SetStationDestination(StationConfig config) { }
        public void SetMapPointDestination(Vector3 point) { }
        public void SetNpcDestination(string id) { }
        public void SetSystemExitDestination(StarSystemLink link) { }
        public void SetSystemExitDestination(RouteExitMapChangedEvent evt) { }
        public void StartTravel() { }
        public void CancelTravel() { CancelCalls++; }
        public void Tick(float seconds, int tick) { }
        public void CompleteTravel() { }
        public void CycleNpcFollowMode() { }
        public float GetNpcFollowModeDistance() => 0;
        public Vector3 GetCurrentDestinationPosition() => Vector3.zero;
        public TravelRoutePreview2A GetCurrentRoutePreview2A(float spacing, int big, int small, float seconds) => default;
        public SystemRouteSectorDebugInfo2A GetRouteSectorDebugInfo2A(Vector3 target) => default;
    }
    private T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); _assets.Add(asset); return asset; }
    private static void Set(object target, string field, object value)
    {
        var type = target.GetType(); FieldInfo member = null;
        while (type != null && member == null) { member = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic); type = type.BaseType; }
        Assert.That(member, Is.Not.Null, field); member.SetValue(target, value);
    }
    [SetUp] public void Setup()
    {
        _session = new Session(); _session.State.Player.CurrentSystemId = "sys";
        _session.State.Player.PlayerShipState.ActiveShipId = "ship";
        _session.State.Player.PlayerShipState.OwnedShips.Add(new ShipRuntimeData { ShipId = "ship", CurrentHull = 50, CurrentShield = 20, HullCapacity = 100 });
        var type = Asset<StationTypeConfig>(); _station = Asset<StationConfig>(); Set(_station, "id", "station");
        Set(_station, "stationTypeConfig", type); Set(_station, "initialLevel", 2);
        _systemConfig = Asset<StarSystemConfig>(); Set(_systemConfig, "id", "sys"); Set(_systemConfig, "station", _station);
        _session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "sys" });
        _events = new SimpleEventBus(); _stations = new StationService(_session, _ => _systemConfig, _events); _stations.InitializeSession(true);
        _movement = new Movement(); _control = new Control(); _travel = new Travel();
        _time = new Clock(); _departureBlocked = false;
        _visit = new StationVisitService(_session, _ => _systemConfig, _stations, _travel, _movement, _control, _events, () => _time, () => _departureBlocked);
    }
    [TearDown] public void Cleanup() { foreach (var a in _assets) UnityEngine.Object.DestroyImmediate(a); _assets.Clear(); }
    [TestCase(1)] [TestCase(10)] public void ArrivalAtWorkingStation_EntersAndDisablesMovement(int level)
    {
        System.Station.Level = level;
        Assert.That(_visit.TryEnter("sys", out _), Is.True);
        Assert.That(_session.State.Player.CurrentStationId, Is.EqualTo("station"));
        Assert.That(_movement.IsEnabled, Is.False); Assert.That(_control.IsEnabled, Is.False);
    }
    [Test] public void DistantStation_CannotBeEntered()
    {
        _movement.SetPosition(Vector2.one * 1000);
        Assert.That(_visit.TryEnter("sys", out string reason), Is.False); Assert.That(reason, Is.EqualTo("ApproachRequired"));
        Assert.That(_session.State.Player.CurrentStationId, Is.Null.Or.Empty);
    }
    [Test] public void UnbuiltStation_CannotBeEntered()
    { System.Station.Level = 0; Assert.That(_visit.TryEnter("sys", out _), Is.False); }
    [Test] public void DestroyedStation_CannotBeEntered()
    { System.ApplyInfrastructureDamageFromWar(100, true); Assert.That(_visit.TryEnter("sys", out _), Is.False); }
    [Test] public void RepeatedEntry_DoesNotPublishAnotherVisitOrSave()
    {
        int visits = 0, saves = 0;
        _events.Subscribe<StationVisitChangedEvent>(evt => visits++); _events.Subscribe<SaveNeedEvent>(evt => saves++);
        _visit.TryEnter("sys", out _); _visit.TryEnter("sys", out _);
        Assert.That(visits, Is.EqualTo(1)); Assert.That(saves, Is.EqualTo(1));
    }
    [Test] public void LeavingStation_RestoresControlAndDoesNotDamageShip()
    {
        _visit.TryEnter("sys", out _); _visit.Leave();
        Assert.That(_control.IsEnabled, Is.True); Assert.That(_movement.IsEnabled, Is.True);
        Assert.That(_session.State.Player.GetActiveShip().CurrentHull, Is.EqualTo(50));
        Assert.That(_session.State.Player.GetActiveShip().CurrentShield, Is.EqualTo(20));
        Assert.That(_session.State.Player.CurrentStationId, Is.Empty);
    }
    [Test] public void Destruction_EjectsOnceWithoutDamage()
    {
        _visit.TryEnter("sys", out _); int ejections = 0;
        _events.Subscribe<StationVisitChangedEvent>(evt => { if (evt.ForcedExit) ejections++; });
        System.ApplyInfrastructureDamageFromWar(100, true); _stations.ReconcileSystem("sys"); _visit.Tick(); _visit.Tick();
        Assert.That(ejections, Is.EqualTo(1)); Assert.That(_session.State.Player.CurrentStationId, Is.Empty);
        Assert.That(_session.State.Player.GetActiveShip().CurrentHull, Is.EqualTo(50)); Assert.That(_control.IsEnabled, Is.True);
    }
    [Test] public void NewDestination_LeavesWithoutCancellingNewRoute()
    {
        _visit.TryEnter("sys", out _);
        _events.Publish(new DestinationSelectedEvent(TravelDestinationType.MapPoint, Vector2.one, "", ""));
        Assert.That(_session.State.Player.CurrentStationId, Is.Empty); Assert.That(_travel.CancelCalls, Is.Zero);
        Assert.That(_control.IsEnabled, Is.True);
    }
    [Test] public void SessionRestore_ReentersFromSavedPosition()
    {
        _visit.TryEnter("sys", out _);
        _session.State.Player.SystemMapShipPosition = Vector3.zero;
        _movement.SetPosition(Vector2.one * 1000);
        _visit.InitializeSession();
        Assert.That(_session.State.Player.CurrentStationId, Is.EqualTo("station"));
        _visit.Leave(); Assert.That(_control.IsEnabled, Is.True);
    }
    [Test] public void StationMarket_CannotLeakIntoPlanetContext()
    {
        var market = Asset<MarketProfileConfig>(); Set(_station, "marketProfileOverride", market);
        Assert.That(StationLocationRules.Market(_session.State, _systemConfig), Is.Null);
        _visit.TryEnter("sys", out _); Assert.That(StationLocationRules.Market(_session.State, _systemConfig), Is.SameAs(market));
        _visit.Leave(); Assert.That(StationLocationRules.Market(_session.State, _systemConfig), Is.Null);
    }
    [Test] public void StationWithoutOverride_UsesLocalInhabitedMarket()
    {
        var market = Asset<MarketProfileConfig>(); var planet = Asset<PlanetConfig>();
        Set(planet, "isInhabited", true); planet.marketProfile = market; Set(_systemConfig, "planetRefs", new[] { planet });
        _visit.TryEnter("sys", out _);
        Assert.That(StationLocationRules.Market(_session.State, _systemConfig), Is.SameAs(market));
    }
    [Test] public void Destruction_ImmediatelyDisablesStationFacilities()
    {
        _visit.TryEnter("sys", out _); Assert.That(StationLocationRules.IsInside(_session.State, _systemConfig), Is.True);
        System.ApplyInfrastructureDamageFromWar(100, true);
        Assert.That(StationLocationRules.IsInside(_session.State, _systemConfig), Is.False);
    }

    private sealed class Clock : IGameTimeService
    {
        public GameTimeState State { get; } = new GameTimeState();
        public int CurrentQuantTick => 0;
        public bool IsPaused => State.IsPaused;
        public float SimulationTimeSeconds => 0;
        public float DelayTime => 0;
        public int Steps;
        public void SetPaused(bool value) { State.IsPaused = value; }
        public void TogglePause() { SetPaused(!IsPaused); }
        public void StepOneDay() { Steps++; }
        public void WriteTimeToSave(GameRuntimeState state) { }
        public void RestoreTimeFromSave(GameRuntimeState state) { }
        public void Tick(float seconds) { }
    }
    private static void Invoke(object target, string method, params object[] args)
    {
        // Reflection avoids Unity SendMessage in EditMode.
        var member = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(member, Is.Not.Null, method); member.Invoke(target, args);
    }
    [TestCase(false)] [TestCase(true)]
    public void StationTakeoff_ReusesPlanetRouteAndRestoresPause(bool originalPause)
    {
        _time.SetPaused(originalPause);
        var pause = new GameTimePauseScopeService(_time, _events);
        _visit.TryEnter("sys", out _);
        Assert.That(_time.IsPaused, Is.True);
        bool mapReady = false;
        _events.Subscribe<StarSystemEnteredEvent>(evt => mapReady = true);
        _travel.BeforeTakeoff = () => {
            Assert.That(mapReady, Is.True, "Map must be restored before creating the route");
            Assert.That(_control.IsEnabled, Is.True);
            Assert.That(_session.State.Player.CurrentStationId, Is.Empty);
        };
        Assert.That(_visit.TryTakeOff(out string reason), Is.True, reason);
        Assert.That(_travel.TakeoffCalls, Is.EqualTo(1));
        Assert.That(_time.IsPaused, Is.EqualTo(originalPause));
        Assert.That(_time.Steps, Is.EqualTo(originalPause ? 1 : 0));
        Assert.That(_movement.State.Position, Is.EqualTo(Vector2.zero), "No teleport to the ejection offset");
        Assert.That(_session.State.Player.GetActiveShip().CurrentHull, Is.EqualTo(50));
        Assert.That(_visit.TryTakeOff(out _), Is.False);
        Assert.That(_travel.TakeoffCalls, Is.EqualTo(1));
    }
    [Test] public void FailedTakeoff_RedocksAndRestoresInteriorPause()
    {
        _time.SetPaused(false); var pause = new GameTimePauseScopeService(_time, _events);
        _visit.TryEnter("sys", out _); _travel.TakeoffAvailable = false;
        Assert.That(_visit.TryTakeOff(out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("TakeoffRouteUnavailable"));
        Assert.That(_session.State.Player.CurrentStationId, Is.EqualTo("station"));
        Assert.That(_movement.IsEnabled, Is.False); Assert.That(_control.IsEnabled, Is.False);
        Assert.That(_time.IsPaused, Is.True); Assert.That(_time.Steps, Is.Zero);
        Assert.That(_movement.State.Position, Is.EqualTo(Vector2.zero));
        _travel.TakeoffAvailable = true;
        Assert.That(_visit.TryTakeOff(out _), Is.True);
        Assert.That(_time.IsPaused, Is.False);
    }
    [Test] public void BlockedTakeoff_DoesNotLeaveOrBuildRoute()
    {
        _visit.TryEnter("sys", out _); _departureBlocked = true;
        int exits = 0; _events.Subscribe<StationVisitChangedEvent>(evt => { if (!evt.Inside) exits++; });
        Assert.That(_visit.TryTakeOff(out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("CargoOverloaded"));
        Assert.That(exits, Is.Zero); Assert.That(_travel.TakeoffCalls, Is.Zero);
        Assert.That(_session.State.Player.CurrentStationId, Is.EqualTo("station"));
        Assert.That(_movement.IsEnabled, Is.False);
    }
    [TestCase(false)] [TestCase(true)]
    public void PauseChosenInsideStation_IsPreservedOnTakeoff(bool selectedPause)
    {
        _time.SetPaused(!selectedPause); var pause = new GameTimePauseScopeService(_time, _events);
        _visit.TryEnter("sys", out _);
        _time.SetPaused(selectedPause); _events.Publish(new GameTimePauseModeSelectedEvent(selectedPause));
        Assert.That(_visit.TryTakeOff(out _), Is.True);
        Assert.That(_time.IsPaused, Is.EqualTo(selectedPause));
    }
    [Test] public void Destruction_RestoresTimeFromStationScope()
    {
        _time.SetPaused(false); var pause = new GameTimePauseScopeService(_time, _events);
        _visit.TryEnter("sys", out _); System.ApplyInfrastructureDamageFromWar(100, true);
        _visit.Tick(); Assert.That(_time.IsPaused, Is.False);
    }
    [Test] public void StationScreen_OpensGovernmentAndClosesOnDeparture()
    {
        var host = new GameObject("LocationController");
        var root = new GameObject("LocationWindow");
        try
        {
            var controller = host.AddComponent<PlanetController>();
            Set(controller, "eventBus", _events); Set(controller, "gameSessionService", _session);
            Set(controller, "planetRoot", root); Set(controller, "_stationVisits", _visit);
            foreach (var field in new[] { "governmentBlock", "marketBlock", "hangarBlock", "fuelBlock" })
            {
                var block = new GameObject(field); block.transform.SetParent(root.transform); Set(controller, field, block);
            }
            foreach (var field in new[] { "openMarketButton", "openGovernmentButton", "openHangarButton", "refuelButton" })
            {
                var button = new GameObject(field, typeof(RectTransform), typeof(Button));
                button.transform.SetParent(root.transform); button.SetActive(false); Set(controller, field, button.GetComponent<Button>());
            }
            int governmentOpened = 0; _events.Subscribe<GovernmentEnteredEvent>(evt => governmentOpened++);
            Invoke(controller, "SubscribeToEvents"); root.SetActive(false);
            _visit.TryEnter("sys", out _);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(root.transform.Find("governmentBlock").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("marketBlock").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("openGovernmentButton").gameObject.activeSelf, Is.True);
            Assert.That(governmentOpened, Is.EqualTo(1));
            Invoke(controller, "OnOpenSystemClicked");
            Assert.That(root.activeSelf, Is.False); Assert.That(_travel.TakeoffCalls, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test] public void StationGovernment_DoesNotRequestNewOfferOrKeepPlanetIllustration()
    {
        var host = new GameObject("Government");
        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        var hero = new GameObject("PlanetHero", typeof(RectTransform));
        try
        {
            _visit.TryEnter("sys", out _);
            var presenter = host.AddComponent<PlanetGovernmentMissionPresenter>();
            var screen = host.AddComponent<MissionScreenController>();
            Set(presenter, "_session", _session); Set(presenter, "_systemConfig", _systemConfig);
            Set(presenter, "_heroFrame", hero.GetComponent<RectTransform>()); Set(presenter, "_missionScreen", screen);
            Set(presenter, "governmentOfferTitleText", title.GetComponent<TMP_Text>());
            title.GetComponent<TMP_Text>().text = "Stale planet offer";
            // No government service is supplied: requesting/generating an offer would fail this test.
            Assert.DoesNotThrow(() => presenter.CheckOffer());
            Assert.That(title.GetComponent<TMP_Text>().text, Is.Empty);
            Assert.That(hero.activeSelf, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(title); UnityEngine.Object.DestroyImmediate(hero); }
    }

    [TestCase(false)] [TestCase(true)]
    public void PlanetPauseScope_RetainsPreviousRestoreBehavior(bool originalPause)
    {
        _time.SetPaused(originalPause); var pause = new GameTimePauseScopeService(_time, _events);
        _events.Publish(new PlanetEnteredEvent("planet"));
        Assert.That(_time.IsPaused, Is.True);
        _events.Publish(new StarSystemEnteredEvent("sys"));
        Assert.That(_time.IsPaused, Is.EqualTo(originalPause));
    }
    [Test] public void UnbuiltStation_DoesNotStartLandingPauseScope()
    {
        _time.SetPaused(false); var pause = new GameTimePauseScopeService(_time, _events);
        System.Station.Level = 0;
        Assert.That(_visit.TryEnter("sys", out _), Is.False);
        Assert.That(_time.IsPaused, Is.False);
    }

    [Test] public void StationHud_UpdatesLocationOnDockingAndLeaving()
    {
        var host = new GameObject("MetaHud");
        var label = new GameObject("Location", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            var hud = host.AddComponent<MetaHudController>();
            var text = label.GetComponent<TMP_Text>();
            Set(hud, "simpleEventBus", _events); Set(hud, "gameSessionService", _session);
            Set(hud, "systemText", text);
            Invoke(hud, "SubscribeToEvents");
            Assert.That(_visit.TryEnter("sys", out _), Is.True);
            Assert.That(text.text, Is.EqualTo("Станция: station (sys)"));
            _visit.Leave();
            Assert.That(text.text, Is.EqualTo("Система: sys"));
            _session.State.Player.CurrentPlanetId = "planet";
            Invoke(hud, "RefreshLocation", _session.State.Player);
            Assert.That(text.text, Is.EqualTo("Планета: planet (sys)"));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(label); }
    }

    [Test] public void StationGovernment_UsesTypeIllustrationAndPlanetLayoutWithoutOverlay()
    {
        var host = new GameObject("Government", typeof(RectTransform));
        var texture = new Texture2D(1881, 836);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1881, 836), Vector2.one * .5f);
        try
        {
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 1600);
            Set(_station.StationTypeConfig, "missionIllustration", sprite);
            _visit.TryEnter("sys", out _);
            var presenter = host.AddComponent<PlanetGovernmentMissionPresenter>();
            var screen = host.AddComponent<MissionScreenController>();
            Set(presenter, "_session", _session); Set(presenter, "_systemConfig", _systemConfig);
            Set(presenter, "_missionScreen", screen);
            Invoke(presenter, "CreateHeroImage");
            var frame = host.transform.Find("GovernmentMissionHero").GetComponent<RectTransform>();
            var illustration = frame.Find("MissionIllustration").GetComponent<Image>();
            var overlay = frame.Find("MissionTabletopOverlay").GetComponent<Image>();
            overlay.sprite = sprite; overlay.enabled = true;
            // No offer service: station art must be independent of offer generation.
            Assert.DoesNotThrow(() => presenter.CheckOffer());
            Assert.That(frame.gameObject.activeSelf, Is.True);
            Assert.That(illustration.sprite, Is.SameAs(sprite));
            Assert.That(illustration.preserveAspect, Is.True);
            Assert.That(frame.anchoredPosition.y, Is.EqualTo(-194f));
            Assert.That(frame.rect.height, Is.EqualTo(640f).Within(.01f));
            Assert.That(overlay.sprite, Is.Null); Assert.That(overlay.enabled, Is.False);
            float inset = (float)typeof(MissionScreenController).GetField("_listTopInset",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
            Assert.That(inset, Is.EqualTo(834f).Within(.01f));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 1600);
            Invoke(presenter, "UpdateHeroLayout");
            Assert.That(frame.rect.height, Is.EqualTo(400f).Within(.01f));
            Set(_station.StationTypeConfig, "missionIllustration", null);
            presenter.CheckOffer();
            Assert.That(frame.gameObject.activeSelf, Is.False);
            Assert.That(illustration.sprite, Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [TestCase("trade", "trade-v1", StationType.Trade)]
    [TestCase("military", "military-v2", StationType.Military)]
    [TestCase("ranger_base", "ranger-v1", StationType.RangerBase)]
    [TestCase("science", "science-v1", StationType.Science)]
    [TestCase("medical", "medical-v1", StationType.Medical)]
    public void StationTypeAsset_HasBoundMissionIllustration(string configKind, string imageKind, StationType expectedType)
    {
        var type = UnityEditor.AssetDatabase.LoadAssetAtPath<StationTypeConfig>(
            "Assets/_Project/Content/Configs/Galaxy/Stations/StationType/station_type_" + configKind + "_01.asset");
        Assert.That(type, Is.Not.Null);
        Assert.That(type.StationType, Is.EqualTo(expectedType));
        Assert.That(type.MissionIllustration, Is.Not.Null);
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(type.MissionIllustration),
            Is.EqualTo("Assets/Art/Missions/Government/government-neutral-station-" + imageKind + "-9x4.png"));
        Assert.That(type.MissionIllustration.rect.width / type.MissionIllustration.rect.height,
            Is.EqualTo(2.25f).Within(.002f));
    }

    [Test] public void DebugDestroy_UsesNormalDamageAndPreservesLevel_AndIsIdempotent()
    {
        int changes = 0, saves = 0;
        _events.Subscribe<StationStateChangedEvent>(evt => changes++);
        _events.Subscribe<SaveNeedEvent>(evt => saves++);
        Assert.That(StationDebugCommands2A.TryDestroyCurrent(_session, _stations, _events, out string reason), Is.True, reason);
        Assert.That(System.Station.IsDestroyed, Is.True); Assert.That(System.Station.IsOperational, Is.False);
        Assert.That(System.Station.CanDock, Is.False); Assert.That(System.Station.Level, Is.EqualTo(2));
        Assert.That(System.InfrastructureDamage, Is.EqualTo(100));
        Assert.That(System.InfrastructureDamageState, Is.EqualTo(SystemInfrastructureDamageState.Destroyed));
        Assert.That(changes, Is.EqualTo(1)); Assert.That(saves, Is.EqualTo(1));
        long revision = System.Station.Revision;
        Assert.That(StationDebugCommands2A.TryDestroyCurrent(_session, _stations, _events, out reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationAlreadyDestroyed"));
        Assert.That(System.Station.Revision, Is.EqualTo(revision));
        Assert.That(changes, Is.EqualTo(1)); Assert.That(saves, Is.EqualTo(1));
    }
    [Test] public void DebugDestroy_DockedPlayerIsEjectedWithoutShipDamage()
    {
        _visit.TryEnter("sys", out _);
        int ejections = 0; _events.Subscribe<StationVisitChangedEvent>(evt => { if (evt.ForcedExit) ejections++; });
        Assert.That(StationDebugCommands2A.TryDestroyCurrent(_session, _stations, _events, out _), Is.True);
        Assert.That(ejections, Is.EqualTo(1)); Assert.That(_session.State.Player.CurrentStationId, Is.Empty);
        Assert.That(_control.IsEnabled, Is.True);
        Assert.That(_session.State.Player.GetActiveShip().CurrentHull, Is.EqualTo(50));
        Assert.That(_session.State.Player.GetActiveShip().CurrentShield, Is.EqualTo(20));
    }
    [Test] public void DebugDestroy_UnbuiltStationDoesNotChangeRuntime()
    {
        System.Station.Level = 0; string before = JsonUtility.ToJson(System);
        Assert.That(StationDebugCommands2A.TryDestroyCurrent(_session, _stations, _events, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationNotBuilt")); Assert.That(JsonUtility.ToJson(System), Is.EqualTo(before));
    }
    [Test] public void DebugDestroy_DoesNotBypassRecoveryProtection()
    {
        System.Station.IsDestroyed = true;
        System.InfrastructureDamage = 100; System.InfrastructureDamageState = SystemInfrastructureDamageState.Destroyed;
        System.Station.Recovery = new StationOperationRuntimeState {
            OperationId = "recovery", Kind = StationOperationKind.Recovery, TargetLevel = 2, DurationSeconds = 10 };
        string before = JsonUtility.ToJson(System);
        Assert.That(StationDebugCommands2A.TryDestroyCurrent(_session, _stations, _events, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationRecoveryProtected")); Assert.That(JsonUtility.ToJson(System), Is.EqualTo(before));
        Assert.That(StationDebugCommands2A.TryDamage100Current(_session, _stations, _events, out reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationRecoveryProtected")); Assert.That(JsonUtility.ToJson(System), Is.EqualTo(before));
    }
    [Test] public void DebugDestroy_PreservesPaidUpgradeProgressAndPausesIt()
    {
        System.Station.Development = new StationOperationRuntimeState {
            OperationId = "paid-upgrade", Kind = StationOperationKind.Upgrade, TargetLevel = 3, DurationSeconds = 30, ElapsedSeconds = 4 };
        Assert.That(StationDebugCommands2A.TryDestroyCurrent(_session, _stations, _events, out _), Is.True);
        Assert.That(System.Station.Level, Is.EqualTo(2));
        Assert.That(System.Station.Development.ElapsedSeconds, Is.EqualTo(4));
        Assert.That(System.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.Destroyed));
    }

    [Test] public void DebugDamage100_RemovesAbsoluteHealthAndKeepsDockedShip()
    {
        _visit.TryEnter("sys", out _); int saves = 0;
        _events.Subscribe<SaveNeedEvent>(evt => saves++);
        int before = System.Station.Health;
        Assert.That(StationDebugCommands2A.TryDamage100Current(_session, _stations, _events, out string reason), Is.True, reason);
        Assert.That(System.Station.Health, Is.EqualTo(before - 100));
        Assert.That(System.Station.MaxHealth, Is.EqualTo(450));
        Assert.That(System.Station.IsDestroyed, Is.False);
        Assert.That(_session.State.Player.CurrentStationId, Is.Not.Empty);
        Assert.That(_session.State.Player.GetActiveShip().CurrentHull, Is.EqualTo(50));
        Assert.That(saves, Is.EqualTo(1));
    }
    [Test] public void DebugDamage100_DestroyedAtZeroAndRepeatedCommandIsRejected()
    {
        System.ApplyInfrastructureDamageFromWar(400, false); _stations.ReconcileSystem("sys");
        Assert.That(StationDebugCommands2A.TryDamage100Current(_session, _stations, _events, out _), Is.True);
        Assert.That(System.Station.Health, Is.Zero); Assert.That(System.Station.IsDestroyed, Is.True);
        long revision = System.Station.Revision;
        Assert.That(StationDebugCommands2A.TryDamage100Current(_session, _stations, _events, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationAlreadyDestroyed"));
        Assert.That(System.Station.Revision, Is.EqualTo(revision));
    }
    [Test] public void DebugDamage100_RejectsUnbuiltAndRecoveringStation()
    {
        System.Station.Level = 0; string before = JsonUtility.ToJson(System);
        Assert.That(StationDebugCommands2A.TryDamage100Current(_session, _stations, _events, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationNotBuilt")); Assert.That(JsonUtility.ToJson(System), Is.EqualTo(before));
        System.Station.Level = 2; System.ApplyInfrastructureDamageFromWar(1, true);
        System.Station.Recovery = new StationOperationRuntimeState { OperationId = "debug-protected",
            Kind = StationOperationKind.Recovery, TargetLevel = 2, DurationSeconds = 10 };
        before = JsonUtility.ToJson(System);
        Assert.That(StationDebugCommands2A.TryDamage100Current(_session, _stations, _events, out reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationRecoveryProtected")); Assert.That(JsonUtility.ToJson(System), Is.EqualTo(before));
    }

    [Test] public void DebugDamage_EmptySerializedRecoveryIsNotMistakenForProtectedRecovery()
    {
        System.Station.Recovery = new StationOperationRuntimeState();
        int health = System.Station.Health;
        Assert.That(StationDebugCommands2A.TryDamage100Current(_session, _stations, _events, out string reason), Is.True, reason);
        Assert.That(System.Station.Recovery, Is.Null);
        Assert.That(System.Station.Health, Is.EqualTo(health - 100));
    }

    [Test] public void StationRead_ClearsEmptyOperationsBeforeValidationWithoutChangingRevision()
    {
        System.Station.Development = new StationOperationRuntimeState(); System.Station.Recovery = new StationOperationRuntimeState();
        long revision = System.Station.Revision;
        Assert.That(_stations.TryGetStation("sys", out var snapshot), Is.True);
        Assert.That(snapshot.Development, Is.Null); Assert.That(snapshot.Recovery, Is.Null);
        Assert.That(System.Station.Revision, Is.EqualTo(revision));
    }
    [Test] public void StationRead_DoesNotDiscardOrAcceptRealOperationWithMissingIdentity()
    {
        System.ApplyInfrastructureDamageFromWar(1, true);
        var invalid = new StationOperationRuntimeState { Kind = StationOperationKind.Recovery, TargetLevel = 2, DurationSeconds = 10 };
        System.Station.Recovery = invalid;
        Assert.Throws<global::System.InvalidOperationException>(() => _stations.TryGetStation("sys", out _));
        Assert.That(System.Station.Recovery, Is.SameAs(invalid)); Assert.That(invalid.DurationSeconds, Is.EqualTo(10));
    }
    [TestCase(false)] [TestCase(true)]
    public void DebugRecovery_RestoresSameLevelAndPaidProgress_AndSavesOnlyOnce(bool legacyRecovery)
    {
        System.Station.Development = new StationOperationRuntimeState {
            OperationId = "paid", Kind = StationOperationKind.Upgrade, TargetLevel = 3,
            DurationSeconds = 30, ElapsedSeconds = 4,
            PaidCosts = new[] { new StationPaymentCostSnapshot { Resource = StationPaymentResource.Diamonds, Amount = 10 } } };
        Assert.That(StationDebugCommands2A.TryDestroyCurrent(_session, _stations, _events, out _), Is.True);
        if (legacyRecovery) System.Station.Recovery = new StationOperationRuntimeState {
            OperationId = "legacy-recovery", Kind = StationOperationKind.Recovery, TargetLevel = 2,
            DurationSeconds = 60, ElapsedSeconds = 7 };
        int changes = 0, saves = 0;
        _events.Subscribe<StationStateChangedEvent>(_ => changes++);
        _events.Subscribe<SaveNeedEvent>(_ => saves++);
        int diamonds = _session.State.Player.Diamonds;
        Assert.That(StationDebugCommands2A.TryRestoreCurrent(_session, _stations, _events, false, out string reason), Is.True, reason);
        Assert.That(System.Station.Level, Is.EqualTo(2));
        Assert.That(System.Station.IsDestroyed, Is.False);
        Assert.That(System.Station.Health, Is.EqualTo(450));
        Assert.That(System.Station.Recovery, Is.Null);
        Assert.That(System.Station.Development.OperationId, Is.EqualTo("paid"));
        Assert.That(System.Station.Development.ElapsedSeconds, Is.EqualTo(4));
        Assert.That(System.Station.Development.PaidCosts[0].Amount, Is.EqualTo(10));
        Assert.That(System.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.None));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(diamonds));
        Assert.That(changes, Is.EqualTo(1)); Assert.That(saves, Is.EqualTo(1));
        string before = JsonUtility.ToJson(_session.State);
        Assert.That(StationDebugCommands2A.TryRestoreCurrent(_session, _stations, _events, false, out reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationNotDestroyed"));
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(before));
        Assert.That(changes, Is.EqualTo(1)); Assert.That(saves, Is.EqualTo(1));
    }

    [TestCase("captured", "SystemCaptured")]
    [TestCase("invasion", "StationUnderAttack")]
    [TestCase("enemies", "StationUnderAttack")]
    [TestCase("unbuilt", "StationNotBuilt")]
    [TestCase("disabled", "StationUnavailable")]
    [TestCase("missing", "StationUnavailable")]
    public void DebugRecovery_RejectionDoesNotChangeWorldOrRequestSave(string condition, string expected)
    {
        System.ApplyInfrastructureDamageFromWar(1, true);
        if (condition == "captured") System.SystemStatus = StarSystemStatus.Captured;
        if (condition == "invasion") System.SystemStatus = StarSystemStatus.Invasion;
        if (condition == "unbuilt") System.Station.Level = 0;
        if (condition == "disabled") System.Station.IsEnabled = false;
        if (condition == "missing") System.Station.ConfigMissing = true;
        int saves = 0; _events.Subscribe<SaveNeedEvent>(_ => saves++);
        string before = JsonUtility.ToJson(_session.State);
        Assert.That(StationDebugCommands2A.TryRestoreCurrent(_session, _stations, _events, condition == "enemies", out string reason), Is.False);
        Assert.That(reason, Is.EqualTo(expected));
        Assert.That(JsonUtility.ToJson(_session.State), Is.EqualTo(before)); Assert.That(saves, Is.Zero);
    }

}
