using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemMapDestinationController : MonoBehaviour
{
    [Header("Click Sources")]
    [SerializeField] private SystemMapClickArea mapClickArea;
    [SerializeField] private List<PlanetSelectableView> planetViews = new();

    public void SetSelectableViews(List<PlanetSelectableView> views)
    {
        Debug.Log("SystemMapDestinationController.SetSelectableViews");
        planetViews = views;
        Debug.Log("SystemMapDestinationController.SetSelectableViews planetViews.Count = " + planetViews.Count);
        Unsubscribe();
        Debug.Log("SystemMapDestinationController.SetSelectableViews Unsubscribe");
        Subscribe();
        Debug.Log("SystemMapDestinationController.SetSelectableViews Subscribe");
    }

    [Header("Visuals")]
    [SerializeField] private SystemDestinationMarkerController markerController;

    [Header("Actions")]
    [SerializeField] private Button flyButton;

    private SimpleEventBus _simpleEventBus;
    private ISystemTravelService _systemTravelService;
    private IOrbitalMotionService _orbitalMotionService;
    private IGameTimeService _gameTimeService;

    private PlanetSelectableView _selectedPlanetView;

    private void Start()
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();

        if (_systemTravelService == null)
            Debug.LogError("[SystemMapDestinationController] ISystemTravelService not found.");

        if (_orbitalMotionService == null)
            Debug.LogError("[SystemMapDestinationController] IOrbitalMotionService not found.");

        if (_gameTimeService == null)
            Debug.LogError("[SystemMapDestinationController] IGameTimeService not found.");

        Subscribe();
        SetFlyButtonActive(false);
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

        // Debug.Log("[SystemMapDestinationController] planetViews.Count = " + planetViews.Count);
        // foreach (PlanetSelectableView planetView in planetViews)
        // {
        //     Debug.Log("[SystemMapDestinationController] planetView = " + planetView);
        //     if (planetView != null)
        //         planetView.Clicked += OnPlanetClicked;
        // }

        // if (systemExitMarkerView != null)
        //     systemExitMarkerView.Clicked += OnSystemExitClicked;

        if (flyButton != null)
            flyButton.onClick.AddListener(OnFlyClicked);

        if (_simpleEventBus != null)
        {
            _simpleEventBus.Subscribe<ExitMapChangedEvent>(OnExitMapChanged);
            _simpleEventBus.Subscribe<PlanetSelectedEvent>(OnPlanetSelected);
        }
    }

    private void Unsubscribe()
    {
        if (mapClickArea != null)
            mapClickArea.EmptyMapClicked -= OnEmptyMapClicked;

        // foreach (PlanetSelectableView planetView in planetViews)
        // {
        //     if (planetView != null)
        //         planetView.Clicked -= OnPlanetClicked;
        // }

        // if (systemExitMarkerView != null)
        //     systemExitMarkerView.Clicked -= OnSystemExitClicked;

        if (flyButton != null)
            flyButton.onClick.RemoveListener(OnFlyClicked);

        if (_simpleEventBus != null)
        {
            _simpleEventBus.Unsubscribe<ExitMapChangedEvent>(OnExitMapChanged);
            _simpleEventBus.Unsubscribe<PlanetSelectedEvent>(OnPlanetSelected);
        }
    }

    private void OnPlanetClicked(PlanetSelectableView planetView)
    {
        Debug.Log($"[SystemMapDestinationController] Planet clicked");
        if (_systemTravelService == null || planetView == null)
            return;

        PlanetConfig planetData = planetView.Planet;
        Debug.Log($"[SystemMapDestinationController] Planet clicked: {planetData.Id}");

        if (planetData == null)
            return;

        _selectedPlanetView = planetView;

        _systemTravelService.SetPlanetDestination(planetData);

        Vector2 position = GetPlanetCurrentPosition(planetData);
        markerController.ShowPlanetDestination(position);

        SetFlyButtonActive(true);

        Debug.Log($"[SystemMapDestinationController] Planet selected: {planetData.Id}");
    }

    private void OnPlanetSelected(PlanetSelectedEvent evt)
    {
        Debug.Log($"[SystemMapDestinationController] Planet clicked");
        // if (_systemTravelService == null || planetView == null)
        if (_systemTravelService == null)
            return;

        PlanetConfig planetData = evt.Planet;
        Debug.Log($"[SystemMapDestinationController] Planet clicked: {planetData.Id}");

        if (planetData == null)
            return;

        foreach (PlanetSelectableView view in planetViews)
            if (view.Planet.Id == planetData.Id)
            {
                _selectedPlanetView = view;
                break;
            }
        // _selectedPlanetView = planetView;

        _systemTravelService.SetPlanetDestination(planetData);

        Vector2 position = GetPlanetCurrentPosition(planetData);
        markerController.ShowPlanetDestination(position);

        SetFlyButtonActive(true);

        Debug.Log($"[SystemMapDestinationController] Planet selected: {planetData.Id}");
    }

    private void OnEmptyMapClicked(Vector2 mapPosition)
    {
        if (_systemTravelService == null)
            return;

        _selectedPlanetView = null;

        _systemTravelService.SetMapPointDestination(mapPosition);
        markerController.ShowMapPointDestination(mapPosition);

        SetFlyButtonActive(true);

        Debug.Log($"[SystemMapDestinationController] Map point selected: {mapPosition}");
    }

    private void OnExitMapChanged(ExitMapChangedEvent evt)
    {
        // StarSystemLink link = evt.StarSystemLink;

        // // if (link == null)
        // // {
        // //     Debug.LogWarning("[SystemMapDestinationController] StarSystemLink is null.");
        // //     return;
        // // }

        // // if (link.LinkedSystem == null)
        // // {
        // //     Debug.LogWarning("[SystemMapDestinationController] Linked system is null.");
        // //     return;
        // // }

        // _selectedPlanetView = null;

        // _systemTravelService.SetSystemExitDestination(link);

        // markerController.ShowSystemExitDestination(link.ExitPoint);

        // SetFlyButtonActive(true);

        // // Debug.Log($"[SystemMapDestinationController] System exit selected: {link.LinkedSystem.Id}");
    }

    private void OnSystemExitClicked(SystemExitNodeView exitMarker)
    {
        // Debug.Log("[SystemMapDestinationController.]");

        if (_systemTravelService == null || exitMarker == null)
            return;

        // StarSystemLink link = exitMarker.Link;
        StarSystemLink link = exitMarker.SystemLink;

        // if (link == null)
        // {
        //     Debug.LogWarning("[SystemMapDestinationController] StarSystemLink is null.");
        //     return;
        // }

        // if (link.LinkedSystem == null)
        // {
        //     Debug.LogWarning("[SystemMapDestinationController] Linked system is null.");
        //     return;
        // }

        _selectedPlanetView = null;

        _systemTravelService.SetSystemExitDestination(link);

        markerController.ShowSystemExitDestination(link.ExitPoint);

        SetFlyButtonActive(true);

        // Debug.Log($"[SystemMapDestinationController] System exit selected: {link.LinkedSystem.Id}");
    }

    private void OnFlyClicked()
    {
        if (_systemTravelService == null)
            return;

        _systemTravelService.StartTravel();
        SetFlyButtonActive(false);
    }

    private void UpdateMovingPlanetDestinationMarker()
    {
        if (_selectedPlanetView == null)
            return;

        if (_systemTravelService == null)
            return;

        if (_systemTravelService.State.Status == SystemTravelStatus.Flying)
            return;

        PlanetConfig planetData = _selectedPlanetView.Planet;

        if (planetData == null)
            return;

        Vector2 position = GetPlanetCurrentPosition(planetData);
        markerController.ShowPlanetDestination(position);
    }

    private Vector2 GetPlanetCurrentPosition(PlanetConfig planetData)
    {
        if (_orbitalMotionService == null || _gameTimeService == null || planetData == null)
            return Vector2.zero;

        return _orbitalMotionService.GetPlanetCurrentPosition(
            planetData.PlanetOrbit);
    }

    private void SetFlyButtonActive(bool active)
    {
        if (flyButton != null)
            flyButton.interactable = active;
    }
}