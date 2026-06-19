using UnityEngine;
using System;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GalaxyMapSystemBuilder : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject galaxyMapRoot;

    [Header("Systems")]
    [SerializeField] private StarSystemNodeView2A starSystemNodePrefab2;
    [SerializeField] private Transform systemNodesContainer;

    [Header("Routes")]
    [SerializeField] private GalaxyMapRoutesBuilder2A routesBuilder;

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

    private StarSystemNodeView2A[] systemNodes;
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

        CreateSystemNodes();

        LogCustom("systemNodeView.Count " + systemNodes.Count());

        SubscribeToEvents();
    }

    public void ClearSelectedSystem()
    {
        if (routesBuilder != null)
            routesBuilder.ClearSelectedPath();

        if (systemNodes != null)
        {
            foreach (StarSystemNodeView2A systemNodeView in systemNodes)
            {
                if (systemNodeView == null)
                    continue;

                systemNodeView.SetSelected(false);
            }
        }
    }

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

    private void AddSystemNode(StarSystemNodeView2A newNode)
    {
        if (systemNodes == null)
        {
            systemNodes = new StarSystemNodeView2A[1];
            systemNodes[0] = newNode;
            return;
        }

        Array.Resize(ref systemNodes, systemNodes.Length + 1);
        systemNodes[systemNodes.Length - 1] = newNode;
    }

    private void OnGalaxyMapSelectionCleared(GalaxyMapSelectionClearedEvent eventData)
    {
        ClearSelectedSystem();
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Subscribe<ExitMapChangedEvent>(OnExitMapChanged);
        eventBus.Subscribe<GalaxyMapSelectionClearedEvent>(OnGalaxyMapSelectionCleared);
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Unsubscribe<ExitMapChangedEvent>(OnExitMapChanged);
        eventBus.Unsubscribe<GalaxyMapSelectionClearedEvent>(OnGalaxyMapSelectionCleared);
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
        foreach (StarSystemNodeView2A systemNodeView in systemNodes)
        {
            LogCustom("systemNodeView = " + systemNodeView);
            systemNodeView.SetState();
        }
    }

    // private void OnSystemClicked(string targetSystemId)
    // {
    //     LogCustom($"Clicked system: {targetSystemId}");

    //     var currentSystemId = gameSessionService.State.Player.CurrentSystemId;

    //     if (string.Equals(currentSystemId, targetSystemId, StringComparison.Ordinal))
    //     {
    //         LogCustom($"{targetSystemId} is the current system.");
    //         _gameStateMachine.Enter(new MetaState());
    //         eventBus.Publish(new StarSystemEnteredEvent(targetSystemId));
    //         return;
    //     }

    //     var result = travelService.GetTravelFailReason(currentSystemId, targetSystemId);
    //     LogCustom($"travel result = {result}");

    //     switch (result)
    //     {
    //         case TravelFailReason.NotEnoughFuel:
    //             ShowMessage("Для перелёта в выбранную систему недостаточно топлива");
    //             return;
    //         case TravelFailReason.SystemsAreNotNeighbors:
    //             ShowMessage("Перелёт в выбранную систему из текущей невозможен");
    //             return;
    //         default:
    //             break;
    //     }

    //     StarSystemLink systemLink = configService.GetCurrentStarSystemLink(targetSystemId);
    //     eventBus.Publish(new ExitMapChangedEvent(systemLink));
    // }

    private void OnSystemClicked(string targetSystemId)
    {
        LogCustom($"Clicked system: {targetSystemId}");

        string currentSystemId = gameSessionService.State.Player.CurrentSystemId;
        System.Collections.Generic.List<string> path = new();
        if (routesBuilder != null)
        {
            path = routesBuilder.ShowShortestPath(
                currentSystemId,
                targetSystemId
            );
        }
        else
        {
            path.Add(currentSystemId);

            if (currentSystemId != targetSystemId)
                path.Add(targetSystemId);
        }

        if (string.Equals(currentSystemId, targetSystemId, StringComparison.Ordinal))
        {
            LogCustom($"{targetSystemId} is the current system.");
            _gameStateMachine.Enter(new MetaState());
            eventBus.Publish(new StarSystemEnteredEvent(targetSystemId));
            return;
        }

        string nextSystemId = targetSystemId;

        if (routesBuilder != null)
        {
            routesBuilder.ShowShortestPath(currentSystemId, targetSystemId);

            if (routesBuilder.HasSelectedPath())
            {
                string firstStepSystemId =
                    routesBuilder.GetNextSystemIdInSelectedPath(currentSystemId);

                if (!string.IsNullOrWhiteSpace(firstStepSystemId))
                    nextSystemId = firstStepSystemId;
            }
            else
            {
                ShowMessage("Маршрут к выбранной системе не найден");
                return;
            }
        }

        TravelFailReason result =
            travelService.GetTravelFailReason(currentSystemId, nextSystemId);

        LogCustom($"travel result = {result}");

        switch (result)
        {
            case TravelFailReason.NotEnoughFuel:
                ShowMessage("Для первого прыжка по маршруту недостаточно топлива");
                break;

            case TravelFailReason.SystemsAreNotNeighbors:
                ShowMessage("Первая система маршрута недоступна");
                break;

            case TravelFailReason.TargetSystemMissing:
                ShowMessage("Целевая система не найдена");
                break;

            case TravelFailReason.CurrentSystemMissing:
                ShowMessage("Текущая система не найдена");
                break;

            default:
                break;
        }

        // StarSystemLink systemLink =
        //     configService.GetCurrentStarSystemLink(nextSystemId);

        // if (systemLink == null)
        // {
        //     ShowMessage("Связь для перелёта не найдена");
        //     return;
        // }

        eventBus.Publish(new GalaxyMapSystemSelectedEvent(
                targetSystemId,
                currentSystemId,
                path,
                nextSystemId,
                result
            ));

        // eventBus.Publish(new ExitMapChangedEvent(systemLink));
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