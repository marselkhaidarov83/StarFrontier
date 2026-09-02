using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemMapDestinationController2 : CustomMonoBehaviour
{
    [Header("Click Sources")]
    [SerializeField] private SystemMapClickArea2 mapClickArea;
    [SerializeField] private List<PlanetSelectableView2> planetViews = new();

    public void SetSelectableViews(List<PlanetSelectableView2> views)
    {
        LogCustom("SetSelectableViews");
        planetViews = views;
        LogCustom("planetViews.Count = " + planetViews.Count);
        Unsubscribe();
        LogCustom("Unsubscribe");
        Subscribe();
        LogCustom("Subscribe");
    }

    [Header("Visuals")]
    [SerializeField] private SystemDestinationMarkerController2 markerController;

    private SimpleEventBus _simpleEventBus;
    private ISystemTravelService _systemTravelService;
    private IOrbitalMotionService _orbitalMotionService;
    private IGameTimeService _gameTimeService;
    private ITargetService2A _targetService;

    private PlanetSelectableView2 _selectedPlanetView;
    private PlanetConfig _selectedPlanetData;

    private void Start()
    {
        LogCustom("Start");

        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();
        _targetService = Bootstrapper.Instance.ServiceRegistry.Get<ITargetService2A>();

        if (_simpleEventBus == null)
            Debug.LogError("[SystemMapDestinationController2] _simpleEventBus not found.");

        if (_systemTravelService == null)
            Debug.LogError("[SystemMapDestinationController2] ISystemTravelService not found.");

        if (_orbitalMotionService == null)
            Debug.LogError("[SystemMapDestinationController2] IOrbitalMotionService not found.");

        if (_gameTimeService == null)
            Debug.LogError("[SystemMapDestinationController2] IGameTimeService not found.");

        Subscribe();
    }

    private void Update()
    {
        UpdateMovingPlanetDestinationMarker();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (mapClickArea != null)
            mapClickArea.EmptyMapClicked += OnEmptyMapClicked;

        if (_simpleEventBus != null)
        {
            _simpleEventBus.Subscribe<RouteExitMapChangedEvent>(OnRouteExitMapChanged);
            _simpleEventBus.Subscribe<PlanetSelectedEvent>(OnPlanetSelected);
            _simpleEventBus.Subscribe<StationSelectedEvent>(OnStationSelected);
            _simpleEventBus.Subscribe<SystemTravelCancelledEvent>(OnTravelCancelled);
            _simpleEventBus.Subscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        }
    }

    private void Unsubscribe()
    {
        if (mapClickArea != null)
            mapClickArea.EmptyMapClicked -= OnEmptyMapClicked;

        if (_simpleEventBus != null)
        {
            _simpleEventBus.Unsubscribe<RouteExitMapChangedEvent>(OnRouteExitMapChanged);
            _simpleEventBus.Unsubscribe<PlanetSelectedEvent>(OnPlanetSelected);
            _simpleEventBus.Unsubscribe<StationSelectedEvent>(OnStationSelected);
            _simpleEventBus.Unsubscribe<SystemTravelCancelledEvent>(OnTravelCancelled);
            _simpleEventBus.Unsubscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        }
    }

    private void OnTravelCancelled(SystemTravelCancelledEvent evt)
    {
        ClearPlanetSelectionAndMarker();
    }

    private void OnTravelCompleted(SystemTravelCompletedEvent evt)
    {
        ClearPlanetSelectionAndMarker();
    }

    private void ClearPlanetSelectionAndMarker()
    {
        _selectedPlanetView =
            null;

        _selectedPlanetData =
            null;

        if (_targetService != null)
        {
            _targetService.ClearTarget();
        }

        if (markerController != null)
        {
            markerController.HideAll();
        }
    }

    private void OnPlanetSelected(PlanetSelectedEvent evt)
    {
        LogCustom("Planet clicked");

        if (_systemTravelService == null)
            return;

        PlanetConfig planetData = evt.Planet;

        if (planetData == null)
            return;

        LogCustom("Planet clicked: " + planetData.Id);

        _selectedPlanetData = planetData;
        _selectedPlanetView = FindPlanetView(planetData.Id);

        _systemTravelService.SetPlanetDestination(planetData);

        Vector3 position = GetPlanetMarkerPosition(planetData);

        if (markerController != null)
            markerController.ShowPlanetDestination(position, planetData);

        LogCustom("Planet selected: " + planetData.Id);
    }

    private void OnStationSelected(StationSelectedEvent evt)
    {
        LogCustom("Station clicked");

        if (_systemTravelService == null)
            return;

        if (evt == null || evt.Station == null)
            return;

        _selectedPlanetView = null;
        _selectedPlanetData = null;

        StationConfig stationData = evt.Station;

        _systemTravelService.SetStationDestination(
            stationData);

        Vector3 position =
            new Vector3(
                stationData.LocalOffset.x,
                stationData.LocalOffset.y,
                0f);

        if (markerController != null)
        {
            markerController.ShowStationDestination(
                position,
                stationData);
        }

        LogCustom("Station selected: " + stationData.Id);
    }

    private PlanetSelectableView2 FindPlanetView(string planetId)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return null;

        if (planetViews == null)
            return null;

        foreach (PlanetSelectableView2 view in planetViews)
        {
            if (view == null)
                continue;

            if (view.Planet == null)
                continue;

            if (view.Planet.Id == planetId)
                return view;
        }

        return null;
    }

    private void OnEmptyMapClicked(Vector3 mapPosition)
    {
        if (_systemTravelService == null)
            return;

        _selectedPlanetView = null;
        _selectedPlanetData = null;

        if (_targetService != null)
            _targetService.ClearTarget();

        _systemTravelService.SetMapPointDestination(mapPosition);

        if (_systemTravelService.State == null ||
            _systemTravelService.State.Destination == null ||
            _systemTravelService.State.Destination.Type != TravelDestinationType.MapPoint)
        {
            if (markerController != null)
                markerController.HideAll();

            return;
        }

        if (markerController != null)
            markerController.ShowMapPointDestination(mapPosition);

        LogCustom("Map point selected: " + mapPosition);
    }

    private void OnRouteExitMapChanged(RouteExitMapChangedEvent evt)
    {
        if (evt == null)
        {
            LogCustom("OnRouteExitMapChanged evt is null");
            return;
        }

        if (evt.RouteConfig == null)
        {
            LogCustom("OnRouteExitMapChanged RouteConfig is null");
            return;
        }

        _selectedPlanetView = null;
        _selectedPlanetData = null;

        LogCustom(
            "OnRouteExitMapChanged.Route = " + evt.RouteConfig.Id +
            " | From = " + evt.FromSystemId +
            " | To = " + evt.ToSystemId
        );

        _systemTravelService.SetSystemExitDestination(evt);

        if (markerController != null)
            markerController.ShowSystemExitDestination(evt);
    }

    private void UpdateMovingPlanetDestinationMarker()
    {
        if (_selectedPlanetData == null)
            return;

        if (markerController == null)
            return;

        Vector3 position = GetPlanetMarkerPosition(_selectedPlanetData);

        markerController.UpdatePlanetDestinationPosition(
            position,
            _selectedPlanetData
        );
    }

    private Vector3 GetPlanetCurrentPosition(PlanetConfig planetData)
    {
        if (_orbitalMotionService == null || _gameTimeService == null || planetData == null)
            return Vector3.zero;

        return _orbitalMotionService.GetPlanetCurrentPosition(planetData.PlanetOrbit);
    }

    private Vector3 GetPlanetMarkerPosition(PlanetConfig planetData)
    {
        if (_selectedPlanetView != null)
            return _selectedPlanetView.transform.position;

        return GetPlanetCurrentPosition(planetData);
    }
}
