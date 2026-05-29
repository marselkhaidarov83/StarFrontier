using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GalaxyMapController2A : CustomMonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private RectTransform systemsRoot;
    [SerializeField] private RectTransform routesRoot;

    [Header("Prefabs")]
    [SerializeField] private GalaxySystemNodeView2A systemNodePrefab;
    [SerializeField] private GalaxyRouteView2A routePrefab;

    [Header("UI")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button travelButton;
    [SerializeField] private TMP_Text selectedSystemText;
    [SerializeField] private TMP_Text currentSystemText;

    private IServiceRegistry _serviceRegistry;
    private IGameSessionService _gameSessionService;
    private IStarSystemService _starSystemService;
    private IRouteService _routeService;
    private IConfigService _configService;
    private IGameStateMachine _gameStateMachine;

    private string _selectedSystemId;

    private readonly List<GameObject> _spawnedObjects = new();

    private void Awake()
    {
        _serviceRegistry = Bootstrapper2A.Instance.ServiceRegistry;

        _configService = _serviceRegistry.Get<IConfigService>();
        _gameSessionService = _serviceRegistry.Get<IGameSessionService>();
        _starSystemService = _serviceRegistry.Get<IStarSystemService>();
        _routeService = _serviceRegistry.Get<IRouteService>();
        _gameStateMachine = _serviceRegistry.Get<IGameStateMachine>();

        closeButton.onClick.AddListener(Close);
        travelButton.onClick.AddListener(OnTravelClicked);

        Refresh();
    }

    public void Close()
    {
        _gameStateMachine.Enter(new MetaState2A());
    }

    private void Refresh()
    {
        ClearSpawnedObjects();

        CreateRoutes();
        CreateSystems();

        UpdateCurrentSystemText();
        SelectSystem(null);
    }

    private void ClearSpawnedObjects()
    {
        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            if (_spawnedObjects[i] != null)
                Destroy(_spawnedObjects[i]);
        }

        _spawnedObjects.Clear();
    }

    private void CreateSystems()
    {
        LogCustom("SectorCount = " + _configService.GalaxyConfig.Sectors.Count);
        foreach (var sectorConfig in _configService.GalaxyConfig.Sectors)
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            LogCustom("SystemCount = " + sectorConfig.Systems.Length);
            foreach (var systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null)
                    continue;

                var systemState = _starSystemService.GetSystem(systemConfig.Id);

                LogCustom("systemState = " + systemState);
                if (systemState == null)
                    continue;   

                LogCustom("systemState.IsDiscovered = " + systemState.IsDiscovered);
                if (!systemState.IsDiscovered)
                    continue;

                var node = Instantiate(systemNodePrefab, systemsRoot);
                LogCustom("node = " + node);

                node.Setup(
                    systemConfig,
                    systemState,
                    _gameSessionService.CurrentSave.GameState.Galaxy.CurrentSystemId == systemConfig.Id,
                    OnSystemClicked
                );

                _spawnedObjects.Add(node.gameObject);
            }
        }
    }

    private void CreateRoutes()
    {
        foreach (var routeConfig in _configService.GalaxyConfig.Routes)
        {
            if (routeConfig == null)
                continue;

            var routeState = _routeService.GetRoute(routeConfig.Id);

            if (routeState == null)
                continue;

            var fromConfig = FindSystemConfig(routeConfig.FromSystemId);
            var toConfig = FindSystemConfig(routeConfig.ToSystemId);

            if (fromConfig == null || toConfig == null)
                continue;

            var fromState = _starSystemService.GetSystem(fromConfig.Id);
            var toState = _starSystemService.GetSystem(toConfig.Id);

            if (fromState == null || toState == null)
                continue;

            if (!fromState.IsDiscovered || !toState.IsDiscovered)
                continue;

            var routeView = Instantiate(routePrefab, routesRoot);

            routeView.Setup(
                fromConfig.MapPosition,
                toConfig.MapPosition,
                routeState.IsUnlocked
            );

            _spawnedObjects.Add(routeView.gameObject);
        }
    }

    private StarSystemConfig FindSystemConfig(string systemId)
    {
        foreach (var sectorConfig in _configService.GalaxyConfig.Sectors)
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (var systemConfig in sectorConfig.Systems)
            {
                if (systemConfig != null && systemConfig.Id == systemId)
                    return systemConfig;
            }
        }

        return null;
    }

    private void OnSystemClicked(string systemId)
    {
        SelectSystem(systemId);
    }

    private void SelectSystem(string systemId)
    {
        _selectedSystemId = systemId;

        if (string.IsNullOrEmpty(systemId))
        {
            selectedSystemText.text = "Select system";
            travelButton.interactable = false;
            return;
        }

        var canTravel = _routeService.CanTravelTo(systemId);

        selectedSystemText.text = systemId;
        travelButton.interactable = canTravel;
    }

    private void OnTravelClicked()
    {
        if (string.IsNullOrEmpty(_selectedSystemId))
            return;

        var travelled = _routeService.TravelTo(_selectedSystemId);

        if (!travelled)
            return;

        Refresh();
    }

    private void UpdateCurrentSystemText()
    {
        var currentSystem = _starSystemService.GetCurrentSystem();

        currentSystemText.text = currentSystem == null
            ? "Current: unknown"
            : $"Current: {currentSystem.Id}";
    }
}
