using UnityEngine;
using System;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GalaxyMapController2A : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject galaxyMapRoot;

    [Header("Sectors")]
    [SerializeField] private GalaxySectorNodeView2 sectorNodePrefab;
    [SerializeField] private Transform sectorNodeContainer;

    [Header("Systems")]
    [SerializeField] private StarSystemNodeView2 starSystemNodePrefab2;
    [SerializeField] private Transform systemNodesContainer;

    [Header("Custom")]
    [SerializeField] private Image errorImage;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private float errorShowDuration = 2f;

    [Header("Camera")]
    [SerializeField] private Camera _camera;
    [SerializeField] private float _cameraSize = 9.6f;
    [SerializeField] private Vector3 _cameraPosition = new Vector3(0, 0, -10f);

    private IConfigService configService;
    private IGameSessionService gameSessionService;
    private SimpleEventBus eventBus;
    private ITravelService travelService;
    private IGameStateMachine _gameStateMachine;

    private StarSystemNodeView2[] systemNodes;
    private SectorLowLayerNodeView[] sectorNodes;
    private Coroutine currentRoutine;

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    public void Initialize()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        travelService = Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();
        _gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();

        // CreateSectors();
        BuildSectors();
        CreateSystemNodes();

        LogCustom("systemNodeView.Count " + systemNodes.Count());

        SubscribeToEvents();
    }

    private void BuildSectors()
    {
        var sectors = configService.GetAllSectors();
        if (sectors == null)
            return;

        foreach (SectorConfig sectorConfig in sectors)
        {
            if (sectorConfig == null)
                continue;

            GalaxySectorNodeView2 view = Instantiate(sectorNodePrefab, sectorNodeContainer);

            view.Initialize(sectorConfig);
        }
    }

    // private void CreateSectors()
    // {
    //     var sectors = configService.GetAllSectors();

    //     LogCustom("sectors.Count " + sectors.Count());
    //     foreach (var sector in sectors)
    //     {
    //         LogCustom("sector add " + sector);

    //         Quaternion spawnRotation = Quaternion.identity;

    //         var node = Instantiate(sectorLowLayerNodePrefab, sectorLowLayerNodeContainer);
    //         node.Initialize(sector);
    //         node.GetComponent<Transform>().position = sector.MapPosition;

    //         LogCustom("MapPosition " + sector.MapPosition.x + " / " + sector.MapPosition.y);
    //         LogCustom("SectorConfig " + sector);

    //         AddSectorLowLayerNode(node);
    //         LogCustom("sectorNodeView.Count " + sectorNodes.Count());
    //     }
    // }

    private void CreateSystemNodes()
    {
        var systems = configService.GetAllStarSystems();

        LogCustom("systems.Count " + systems.Count());
        foreach (var system in systems)
        {
            LogCustom("system add " + system);

            Quaternion spawnRotation = Quaternion.identity;

            var node = Instantiate(starSystemNodePrefab2, systemNodesContainer);
            node.Initialize(system, OnSystemClicked);
            node.GetComponent<Transform>().position =
                    new Vector2(system.MapPosition.x, system.MapPosition.y);

            LogCustom("MapPosition " + system.MapPosition.x + " / " + system.MapPosition.y);
            LogCustom("SystemConfig " + system);

            AddSystemNode(node);
            LogCustom("systemNodeView.Count " + systemNodes.Count());
        }
    }

    private void AddSectorLowLayerNode(SectorLowLayerNodeView newNode)
    {
        if (sectorNodes == null)
        {
            sectorNodes = new SectorLowLayerNodeView[1];
            sectorNodes[0] = newNode;
            return;
        }

        Array.Resize(ref sectorNodes, sectorNodes.Length + 1);
        sectorNodes[sectorNodes.Length - 1] = newNode;
    }

    private void AddSystemNode(StarSystemNodeView2 newNode)
    {
        if (systemNodes == null)
        {
            systemNodes = new StarSystemNodeView2[1];
            systemNodes[0] = newNode;
            return;
        }

        Array.Resize(ref systemNodes, systemNodes.Length + 1);
        systemNodes[systemNodes.Length - 1] = newNode;
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Subscribe<ExitMapChangedEvent>(OnExitMapChanged);
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Unsubscribe<ExitMapChangedEvent>(OnExitMapChanged);
    }


    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        if (_camera != null)
        {
            _camera.orthographicSize = _cameraSize;
            _camera.transform.position = _cameraPosition;
        }
        Refresh();
    }

    public void Refresh()
    {
        LogCustom("Refresh");
        foreach (StarSystemNodeView2 systemNodeView in systemNodes)
        {
            LogCustom("systemNodeView = " + systemNodeView);
            systemNodeView.SetState();
        }
    }

    private void OnSystemClicked(string targetSystemId)
    {
        LogCustom($"Clicked system: {targetSystemId}");

        var currentSystemId = gameSessionService.State.Player.CurrentSystemId;

        if (string.Equals(currentSystemId, targetSystemId, StringComparison.Ordinal))
        {
            LogCustom($"{targetSystemId} is the current system.");
            _gameStateMachine.Enter(new MetaState());
            eventBus.Publish(new StarSystemEnteredEvent(targetSystemId));
            return;
        }

        var result = travelService.GetTravelFailReason(currentSystemId, targetSystemId);
        LogCustom($"travel result = {result}");

        switch (result)
        {
            case TravelFailReason.NotEnoughFuel:
                ShowMessage("Для перелёта в выбранную систему недостаточно топлива");
                return;
            case TravelFailReason.SystemsAreNotNeighbors:
                ShowMessage("Перелёт в выбранную систему из текущей невозможен");
                return;
            default:
                break;
        }

        StarSystemLink systemLink = configService.GetCurrentStarSystemLink(targetSystemId);
        eventBus.Publish(new ExitMapChangedEvent(systemLink));
    }

    private void OnExitMapChanged(ExitMapChangedEvent evt)
    {
        galaxyMapRoot?.SetActive(false);
    }

    public void ShowMessage(string text)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowMessageRoutine(text));
    }

    private IEnumerator ShowMessageRoutine(string text)
    {
        if (errorImage != null)
            errorImage.gameObject.SetActive(true);

        if (errorText != null)
            errorText.text = text;

        yield return new WaitForSeconds(errorShowDuration);

        if (errorImage != null)
            errorImage.gameObject.SetActive(false);

        currentRoutine = null;
    }
}