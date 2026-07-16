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

    private PlanetSelectableView2 _selectedPlanetView;

    private void Start()
    {
        LogCustom("Start");

        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();

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
            _simpleEventBus.Unsubscribe<SystemTravelCancelledEvent>(OnTravelCancelled);
            _simpleEventBus.Unsubscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        }
    }

    private void OnTravelCancelled(SystemTravelCancelledEvent evt)
    {
        _selectedPlanetView = null;

        if (markerController != null)
            markerController.HideAll();
    }

    private void OnTravelCompleted(SystemTravelCompletedEvent evt)
    {
        _selectedPlanetView = null;

        if (markerController != null)
            markerController.HideAll();
    }

    private void OnPlanetSelected(PlanetSelectedEvent evt)
    {
        LogCustom($"Planet clicked");
        if (_systemTravelService == null)
            return;

        PlanetConfig planetData = evt.Planet;
        LogCustom($"Planet clicked: {planetData.Id}");

        if (planetData == null)
            return;

        foreach (PlanetSelectableView2 view in planetViews)
            if (view.Planet.Id == planetData.Id)
            {
                _selectedPlanetView = view;
                break;
            }
        // _selectedPlanetView = planetView;

        _systemTravelService.SetPlanetDestination(planetData);

        Vector3 position = GetPlanetCurrentPosition(planetData);
        LogCustom("position = " + position);
        markerController.ShowPlanetDestination(position, planetData);

        LogCustom($"LogCustomPlanet selected: {planetData.Id}");
    }

    private void OnEmptyMapClicked(Vector3 mapPosition)
    {
        if (_systemTravelService == null)
            return;

        _selectedPlanetView = null;

        _systemTravelService.SetMapPointDestination(mapPosition);
        markerController.ShowMapPointDestination(mapPosition);

        LogCustom($"LogCustomMap point selected: {mapPosition}");
    }

    // private void OnExitMapChanged(ExitMapChangedEvent evt)
    // {
    //     StarSystemLink link = evt.StarSystemLink;
    //     LogCustom("OnExitMapChanged.StarSystemLink = " + link.LinkedSystem.DisplayName);

    //     _selectedPlanetView = null;

    //     _systemTravelService.SetSystemExitDestination(link);

    //     markerController.ShowSystemExitDestination(link);

    //     SetFlyButtonActive(true);
    // }

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

        LogCustom(
            "OnRouteExitMapChanged.Route = " +
            evt.RouteConfig.Id +
            " | From = " +
            evt.FromSystemId +
            " | To = " +
            evt.ToSystemId
        );

        _selectedPlanetView = null;

        _systemTravelService.SetSystemExitDestination(evt);

        markerController.ShowSystemExitDestination(evt);
    }

    private void UpdateMovingPlanetDestinationMarker()
    {
        if (_selectedPlanetView == null)
            return;

        if (_systemTravelService == null)
            return;

        PlanetConfig planetData = _selectedPlanetView.Planet;

        if (planetData == null)
            return;

        Vector3 position = GetPlanetCurrentPosition(planetData);

        markerController.UpdatePlanetDestinationPosition(position, planetData);
    }

    private Vector3 GetPlanetCurrentPosition(PlanetConfig planetData)
    {
        if (_orbitalMotionService == null || _gameTimeService == null || planetData == null)
            return Vector3.zero;

        return _orbitalMotionService.GetPlanetCurrentPosition(planetData.PlanetOrbit);
    }
}