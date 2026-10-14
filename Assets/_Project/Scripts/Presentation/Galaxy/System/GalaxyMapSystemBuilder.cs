using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GalaxyMapSystemBuilder : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField]
    private GameObject galaxyMapRoot;

    [Header("Systems")]
    [SerializeField]
    private StarSystemNodeView2A starSystemNodePrefab2;

    [SerializeField]
    private Transform systemNodesContainer;

    [Header("Routes")]
    [SerializeField]
    private GalaxyMapRoutesBuilder2A routesBuilder;

    [Header("Custom")]
    [SerializeField]
    private Image errorImage;

    [SerializeField]
    private TMP_Text errorText;

    [SerializeField]
    private float errorShowDuration = 2f;

    [Header("Invasion Warning Limits")]
    [SerializeField]
    private float invasionWarningCooldownSeconds = 12f;

    [SerializeField]
    private int maxInvasionWarningsPerMapSession = 1;

    [Header("Adaptive Camera")]
    [SerializeField]
    private GalaxyMapViewFitter mapViewFitter;

    [SerializeField]
    private MapCameraController2A mapCameraController;

    [SerializeField]
    private GalaxySectorVisualFitter2A sectorVisualFitter;

    private IConfigService configService;
    private IGameSessionService gameSessionService;
    private ITravelService travelService;
    private ISystemTravelService systemTravelService;
    private IInvasionService invasionService;
    private IGameStateMachine gameStateMachine;
    private SimpleEventBus eventBus;

    private string lastShownInvasionWarningId;
    private float lastInvasionWarningShownAt = -999f;
    private int invasionWarningsShownThisMapSession;

    private StarSystemNodeView2A[] systemNodes;
    private Coroutine currentRoutine;

    private bool isInitialized;

    public void Initialize()
    {
        if (isInitialized)
            return;

        if (Bootstrapper.Instance == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "Bootstrapper.Instance is null.");

            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "ServiceRegistry is null.");

            return;
        }

        eventBus =
            Bootstrapper.Instance.ServiceRegistry
                .Get<SimpleEventBus>();

        gameSessionService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameSessionService>();

        configService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IConfigService>();

        travelService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<ITravelService>();

        systemTravelService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<ISystemTravelService>();

        invasionService =
            Bootstrapper.Instance.ServiceRegistry
            .Get<IInvasionService>();

        gameStateMachine =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameStateMachine>();

        CreateSystemNodes();
        SubscribeToEvents();

        isInitialized = true;

        StartCoroutine(ApplyAdaptiveCamera());

        LogCustom(
            "Galaxy map initialized. " +
            "System node count = " +
            (systemNodes != null ? systemNodes.Length : 0)
        );
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<ExitMapChangedEvent>(
            OnExitMapChanged
        );

        eventBus.Subscribe<GalaxyMapSelectionClearedEvent>(
            OnGalaxyMapSelectionCleared
        );
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<ExitMapChangedEvent>(
            OnExitMapChanged
        );

        eventBus.Unsubscribe<GalaxyMapSelectionClearedEvent>(
            OnGalaxyMapSelectionCleared
        );
    }

    private void CreateSystemNodes()
    {
        if (configService == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "IConfigService is null.");

            return;
        }

        if (starSystemNodePrefab2 == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "Star System Node Prefab 2 is not assigned.");

            return;
        }

        if (systemNodesContainer == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "System Nodes Container is not assigned.");

            return;
        }

        IReadOnlyList<StarSystemConfig> systems =
            configService.GetAllStarSystems();

        if (systems == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "Star-system config collection is null.");

            return;
        }

        foreach (StarSystemConfig systemConfig in systems)
        {
            if (systemConfig == null)
                continue;

            StarSystemNodeView2A node = Instantiate(
                starSystemNodePrefab2,
                systemNodesContainer
            );

            node.Initialize(
                systemConfig,
                OnSystemClicked
            );

            node.transform.position = new Vector3(
                systemConfig.MapPosition.x,
                systemConfig.MapPosition.y,
                0f
            );

            AddSystemNode(node);
        }
    }

    private void AddSystemNode(
        StarSystemNodeView2A newNode
    )
    {
        if (newNode == null)
            return;

        if (systemNodes == null)
        {
            systemNodes =
                new StarSystemNodeView2A[1];

            systemNodes[0] = newNode;
            return;
        }

        Array.Resize(
            ref systemNodes,
            systemNodes.Length + 1
        );

        systemNodes[systemNodes.Length - 1] =
            newNode;
    }

    public void ClearSelectedSystem()
    {
        if (routesBuilder != null)
            routesBuilder.ClearSelectedPath();

        SetSelectedSystemVisual(
            string.Empty
        );
    }

    private void OnGalaxyMapSelectionCleared(
        GalaxyMapSelectionClearedEvent eventData
    )
    {
        ClearSelectedSystem();
    }

    private IEnumerator ApplyAdaptiveCamera()
    {
        yield return new WaitForEndOfFrame();

        if (mapViewFitter == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "MapViewFitter is not assigned.");

            yield break;
        }

        if (mapCameraController == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                typeof(GalaxyMapSystemBuilder),
                "MapCameraController is not assigned.");

            yield break;
        }

        float fitSize =
            mapViewFitter.FitNow();

        mapCameraController.SetZoomOutLimit(
            fitSize,
            applyImmediately: true
        );

        if (sectorVisualFitter != null)
            sectorVisualFitter.FitNow();
    }

    public void Refresh()
    {
        if (systemNodes != null)
        {
            foreach (
                StarSystemNodeView2A systemNodeView
                in systemNodes
            )
            {
                if (systemNodeView == null)
                    continue;

                systemNodeView.SetState();
            }
        }
    }

    /*
     * 2A-S06-04-T07:
     * The galaxy map only reads active invasions to show player-facing
     * warnings. It must not start, resolve, cancel, or mutate invasion
     * state from UI code.
     */
    private void ShowActiveInvasionWarning()
    {
        if (invasionService == null)
            return;

        if (!CanShowInvasionWarningNow())
            return;

        if (!TryGetPriorityInvasion(
                out InvasionState invasionState))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                invasionState.InvasionId))
        {
            return;
        }

        if (string.Equals(
                lastShownInvasionWarningId,
                invasionState.InvasionId,
                StringComparison.Ordinal))
        {
            return;
        }

        MarkInvasionWarningShown(
            invasionState.InvasionId
        );

        ShowMessage(
            BuildInvasionWarningText(invasionState)
        );
    }

    private bool CanShowInvasionWarningNow()
    {
        if (maxInvasionWarningsPerMapSession <= 0)
            return false;

        if (invasionWarningsShownThisMapSession >=
            maxInvasionWarningsPerMapSession)
        {
            return false;
        }

        float cooldown =
            Mathf.Max(
                0f,
                invasionWarningCooldownSeconds
            );

        if (Time.unscaledTime - lastInvasionWarningShownAt <
            cooldown)
        {
            return false;
        }

        return true;
    }

    private void MarkInvasionWarningShown(
        string invasionId)
    {
        lastShownInvasionWarningId =
            invasionId;

        lastInvasionWarningShownAt =
            Time.unscaledTime;

        invasionWarningsShownThisMapSession++;
    }

    private bool TryGetPriorityInvasion(
        out InvasionState priorityInvasion)
    {
        priorityInvasion = null;

        if (invasionService == null)
            return false;

        IReadOnlyList<InvasionState> invasions =
            invasionService.GetActiveInvasions();

        if (invasions == null ||
            invasions.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < invasions.Count; i++)
        {
            InvasionState invasion =
                invasions[i];

            if (invasion == null)
                continue;

            if (!invasion.IsActive())
                continue;

            if (priorityInvasion == null ||
                GetInvasionPriority(invasion) >
                GetInvasionPriority(priorityInvasion))
            {
                priorityInvasion = invasion;
            }
        }

        return priorityInvasion != null;
    }

    private int GetInvasionPriority(
        InvasionState invasionState)
    {
        if (invasionState == null)
            return 0;

        int priority =
            invasionState.EscalationPressure;

        if (invasionState.LifecycleState ==
            InvasionLifecycleState.Active)
        {
            priority += 100;
        }

        if (invasionState.LifecycleState ==
            InvasionLifecycleState.Preparing)
        {
            priority += 50;
        }

        return priority;
    }

    private string BuildInvasionWarningText(
        InvasionState invasionState)
    {
        string systemName =
            GetSystemDisplayName(
                invasionState.TargetSystemId
            );

        string factionName =
            GetFactionDisplayName(
                invasionState.FactionId
            );

        string description =
            GetFactionThreatDescription(
                invasionState.FactionId
            );

        return
            "Вторжение: " +
            systemName +
            "\nВраг: " +
            factionName +
            "\n" +
            description;
    }

    private string GetSystemDisplayName(
        string systemId)
    {
        if (configService != null &&
            configService.TryGetStarSystem(
                systemId,
                out StarSystemConfig systemConfig) &&
            systemConfig != null &&
            !string.IsNullOrWhiteSpace(
                systemConfig.DisplayName))
        {
            return systemConfig.DisplayName;
        }

        return string.IsNullOrWhiteSpace(systemId)
            ? "неизвестная система"
            : systemId;
    }

    private string GetFactionDisplayName(
        string factionId)
    {
        string normalizedFactionId =
            NormalizeFactionId(factionId);

        switch (normalizedFactionId)
        {
            case "ancients":
                return "Древние";

            case "ai":
                return "Враждебный ИИ";

            case "infected":
                return "Заражённые";

            default:
                return string.IsNullOrWhiteSpace(factionId)
                    ? "неизвестная фракция"
                    : factionId;
        }
    }

    private string GetFactionThreatDescription(
        string factionId)
    {
        string normalizedFactionId =
            NormalizeFactionId(factionId);

        switch (normalizedFactionId)
        {
            case "ancients":
                return "Древняя сила атакует систему. Подготовься к тяжёлому бою.";

            case "ai":
                return "Боевые группы ИИ входят в систему. Реакция нужна быстро.";

            case "infected":
                return "Заражение распространяется через систему. Промедление усилит угрозу.";

            default:
                return "Вражеские силы атакуют систему. Проверь карту и подготовь маршрут.";
        }
    }

    private string NormalizeFactionId(
        string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId)
            ? string.Empty
            : factionId.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Восстанавливает на карте галактики систему,
    /// которая была выбрана через точку выхода
    /// на системной карте.
    /// </summary>
    public bool RestoreSelectionFromSystemTravel()
    {
        if (!isInitialized)
        {
            LogCustom(
                "[GalaxyMapSystemBuilder] " +
                "Restore skipped: builder is not initialized."
            );

            return false;
        }

        if (systemTravelService == null)
            return false;

        SystemTravelState travelState =
            systemTravelService.State;

        if (travelState == null)
            return false;

        if (!travelState.HasDestination)
            return false;

        if (travelState.Destination == null)
            return false;

        if (travelState.Destination.Type !=
            TravelDestinationType.SystemExit)
        {
            return false;
        }

        string targetSystemId =
            travelState.Destination.TargetSystemId;

        if (string.IsNullOrWhiteSpace(
                targetSystemId
            ))
        {
            return false;
        }

        string currentSystemId =
            GetCurrentSystemId();

        if (string.IsNullOrWhiteSpace(
                currentSystemId
            ))
        {
            return false;
        }

        if (string.Equals(
                currentSystemId,
                targetSystemId,
                StringComparison.Ordinal
            ))
        {
            return false;
        }

        LogCustom(
            "[GalaxyMapSystemBuilder] " +
            "Restoring system-map destination. " +
            "CurrentSystemId = " +
            currentSystemId +
            " | TargetSystemId = " +
            targetSystemId
        );

        return SelectSystem(
            targetSystemId,
            openCurrentSystem: false
        );
    }

    private void OnSystemClicked(
        string targetSystemId
    )
    {
        SelectSystem(
            targetSystemId,
            openCurrentSystem: true
        );
    }

    /// <summary>
    /// Общая логика выбора системы.
    /// Используется и ручным нажатием на карте галактики,
    /// и восстановлением выбора с системной карты.
    /// </summary>
    private bool SelectSystem(
        string targetSystemId,
        bool openCurrentSystem
    )
    {
        float startedAt =
            Time.realtimeSinceStartup;

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.Start" +
            " | TargetSystemId=" + targetSystemId +
            " | OpenCurrentSystem=" + openCurrentSystem +
            " | Time=" + startedAt.ToString("F3"));

        if (string.IsNullOrWhiteSpace(targetSystemId))
        {
            LogCustom("[GALAXY_CLICK_TRACE] SelectSystem.End | Reason=EmptyTarget");
            return false;
        }

        if (gameSessionService == null ||
            gameSessionService.State == null ||
            gameSessionService.State.Player == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                "[GALAXY_CLICK_TRACE] SelectSystem.End | Reason=MissingPlayerState");

            return false;
        }

        if (travelService == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                "[GALAXY_CLICK_TRACE] SelectSystem.End | Reason=MissingTravelService");

            return false;
        }

        string currentSystemId =
            GetCurrentSystemId();

        if (string.IsNullOrWhiteSpace(currentSystemId))
        {
            ShowMessage("Текущая система не найдена");

            LogCustom(
                "[GALAXY_CLICK_TRACE] SelectSystem.End | Reason=EmptyCurrentSystem");

            return false;
        }

        bool isCurrentSystem =
            string.Equals(
                currentSystemId,
                targetSystemId,
                StringComparison.Ordinal);

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.Context" +
            " | CurrentSystemId=" + currentSystemId +
            " | TargetSystemId=" + targetSystemId +
            " | IsCurrentSystem=" + isCurrentSystem +
            " | OpenCurrentSystem=" + openCurrentSystem);

        if (isCurrentSystem)
        {
            LogCustom(
                "[GALAXY_CLICK_TRACE] SelectSystem.CurrentSystemBranch" +
                " | WillEnterSystemState=" + openCurrentSystem +
                " | WillBuildPath=False" +
                " | WillCallTravelFailReason=False");

            if (openCurrentSystem)
            {
                LogCustom(
                    "[GALAXY_CLICK_TRACE] EnterSystemState.Before" +
                    " | Reason=CurrentSystemClick" +
                    " | SystemId=" + currentSystemId);

                gameStateMachine?.Enter(
                    new SystemState()
                );

                LogCustom(
                    "[GALAXY_CLICK_TRACE] EnterSystemState.After" +
                    " | Reason=CurrentSystemClick" +
                    " | SystemId=" + currentSystemId);

                eventBus?.Publish(
                    new StarSystemEnteredEvent(
                        currentSystemId
                    )
                );
            }
            else
            {
                ClearSelectedSystem();
            }

            LogCustom(
                "[GALAXY_CLICK_TRACE] SelectSystem.End" +
                " | Reason=CurrentSystem" +
                " | DurationMs=" + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("F2"));

            return false;
        }

        float pathStartedAt =
            Time.realtimeSinceStartup;

        List<string> path =
            BuildPath(
                currentSystemId,
                targetSystemId
            );

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.AfterBuildPath" +
            " | PathCount=" + (path != null ? path.Count : -1) +
            " | Path=" + FormatPathForGalaxyClickTrace(path) +
            " | DurationMs=" + ((Time.realtimeSinceStartup - pathStartedAt) * 1000f).ToString("F2"));

        if (path == null || path.Count < 2)
        {
            SetSelectedSystemVisual(string.Empty);
            ShowMessage("Маршрут к выбранной системе не найден");

            LogCustom(
                "[GALAXY_CLICK_TRACE] SelectSystem.End" +
                " | Reason=PathMissing" +
                " | DurationMs=" + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("F2"));

            return false;
        }

        string nextSystemId =
            GetNextSystemId(
                currentSystemId,
                targetSystemId
            );

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.NextSystem" +
            " | NextSystemId=" + nextSystemId);

        if (string.IsNullOrWhiteSpace(nextSystemId))
        {
            SetSelectedSystemVisual(string.Empty);
            ShowMessage("Первая система маршрута не найдена");

            LogCustom(
                "[GALAXY_CLICK_TRACE] SelectSystem.End | Reason=NextSystemMissing");

            return false;
        }

        float failReasonStartedAt =
            Time.realtimeSinceStartup;

        TravelFailReason result =
            travelService.GetTravelFailReason(
                currentSystemId,
                nextSystemId
            );

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.AfterGetTravelFailReason" +
            " | From=" + currentSystemId +
            " | To=" + nextSystemId +
            " | Result=" + result +
            " | DurationMs=" + ((Time.realtimeSinceStartup - failReasonStartedAt) * 1000f).ToString("F2"));

        ShowTravelWarning(result);

        float visualStartedAt =
            Time.realtimeSinceStartup;

        SetSelectedSystemVisual(targetSystemId);

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.AfterSetSelectedSystemVisual" +
            " | DurationMs=" + ((Time.realtimeSinceStartup - visualStartedAt) * 1000f).ToString("F2"));

        eventBus?.Publish(
            new GalaxyMapSystemSelectedEvent(
                targetSystemId,
                currentSystemId,
                path,
                nextSystemId,
                result
            )
        );

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.TargetSelectedWithoutOpeningSystem" +
            " | TargetSystemId=" + targetSystemId +
            " | NextSystemId=" + nextSystemId +
            " | TravelFailReason=" + result);

        LogCustom(
            "[GALAXY_CLICK_TRACE] SelectSystem.End" +
            " | Reason=SelectedTargetOnly" +
            " | DurationMs=" + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("F2"));

        return true;
    }

    private void OpenCurrentSystemWithRouteDestination(
        string currentSystemId,
        string nextSystemId)
    {
        float startedAt =
            Time.realtimeSinceStartup;

        LogCustom(
            "[GALAXY_CLICK_TRACE] OpenCurrentSystemWithRouteDestination.Start" +
            " | CurrentSystemId=" + currentSystemId +
            " | NextSystemId=" + nextSystemId +
            " | HasSystemTravelService=" + (systemTravelService != null));

        if (systemTravelService == null)
        {
            AppLog.Error(DebugLogChannel.GalaxyMap,
                "[GALAXY_CLICK_TRACE] OpenCurrentSystemWithRouteDestination.End | Reason=MissingSystemTravelService");

            return;
        }

        RouteConfig routeConfig =
            FindRouteConfig(
                currentSystemId,
                nextSystemId
            );

        LogCustom(
            "[GALAXY_CLICK_TRACE] OpenCurrentSystemWithRouteDestination.AfterFindRoute" +
            " | RouteId=" + (routeConfig != null ? routeConfig.Id : "NULL"));

        if (routeConfig == null)
        {
            ShowMessage("Маршрут к первой системе не найден");
            return;
        }

        Vector3 exitPoint =
            routeConfig.GetExitPoint(currentSystemId);

        Vector3 entryPoint =
            routeConfig.GetEntryPoint(nextSystemId);

        RouteEndpointConfig departureEndpoint =
            routeConfig.GetDepartureEndpoint(currentSystemId);

        LogCustom(
            "[GALAXY_CLICK_TRACE] SetSystemExitDestination.Before" +
            " | RouteId=" + routeConfig.Id +
            " | From=" + currentSystemId +
            " | To=" + nextSystemId +
            " | ExitPoint=" + exitPoint +
            " | EntryPoint=" + entryPoint);

        systemTravelService.SetSystemExitDestination(
            new RouteExitMapChangedEvent(
                routeConfig,
                currentSystemId,
                nextSystemId,
                exitPoint,
                entryPoint,
                departureEndpoint != null
                    ? departureEndpoint.VisualSize
                    : 0f
            )
        );

        LogCustom(
            "[GALAXY_CLICK_TRACE] SetSystemExitDestination.After");

        LogCustom(
            "[GALAXY_CLICK_TRACE] EnterSystemState.Before" +
            " | Reason=TargetSystemClick" +
            " | CurrentSystemId=" + currentSystemId +
            " | NextSystemId=" + nextSystemId);

        gameStateMachine?.Enter(
            new SystemState()
        );

        LogCustom(
            "[GALAXY_CLICK_TRACE] EnterSystemState.After" +
            " | Reason=TargetSystemClick" +
            " | DurationMs=" + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("F2"));

        eventBus?.Publish(
            new StarSystemEnteredEvent(
                currentSystemId
            )
        );
    }

    private static string FormatPathForGalaxyClickTrace(
        IReadOnlyList<string> path)
    {
        if (path == null)
            return "NULL";

        if (path.Count == 0)
            return "EMPTY";

        return string.Join(">", path);
    }

    private RouteConfig FindRouteConfig(
        string currentSystemId,
        string nextSystemId)
    {
        if (configService == null)
            return null;

        if (string.IsNullOrWhiteSpace(currentSystemId) ||
            string.IsNullOrWhiteSpace(nextSystemId))
        {
            return null;
        }

        StarSystemConfig currentSystem =
            configService.GetStarSystemConfigById(currentSystemId);

        if (currentSystem == null ||
            currentSystem.Routes == null)
        {
            return null;
        }

        foreach (RouteConfig routeConfig in currentSystem.Routes)
        {
            if (routeConfig == null)
                continue;

            StarSystemConfig otherSystem =
                routeConfig.GetOtherSystem(currentSystemId);

            if (otherSystem == null)
                continue;

            if (string.Equals(
                    otherSystem.Id,
                    nextSystemId,
                    StringComparison.Ordinal))
            {
                return routeConfig;
            }
        }

        return null;
    }

    private List<string> BuildPath(
        string currentSystemId,
        string targetSystemId
    )
    {
        float startedAt =
            Time.realtimeSinceStartup;

        LogCustom(
            "[GALAXY_CLICK_TRACE] BuildPath.Start" +
            " | CurrentSystemId=" + currentSystemId +
            " | TargetSystemId=" + targetSystemId +
            " | HasRoutesBuilder=" + (routesBuilder != null));

        if (routesBuilder != null)
        {
            List<string> selectedPath =
                routesBuilder.ShowShortestPath(
                    currentSystemId,
                    targetSystemId
                );

            bool hasSelectedPath =
                routesBuilder.HasSelectedPath();

            LogCustom(
                "[GALAXY_CLICK_TRACE] BuildPath.AfterShowShortestPath" +
                " | HasSelectedPath=" + hasSelectedPath +
                " | PathCount=" + (selectedPath != null ? selectedPath.Count : -1) +
                " | Path=" + FormatPathForGalaxyClickTrace(selectedPath) +
                " | DurationMs=" + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("F2"));

            if (!hasSelectedPath)
                return new List<string>();

            return selectedPath != null
                ? new List<string>(selectedPath)
                : new List<string>();
        }

        List<string> fallbackPath =
            new List<string>
            {
            currentSystemId
            };

        if (!string.Equals(
                currentSystemId,
                targetSystemId,
                StringComparison.Ordinal))
        {
            fallbackPath.Add(targetSystemId);
        }

        LogCustom(
            "[GALAXY_CLICK_TRACE] BuildPath.Fallback" +
            " | Path=" + FormatPathForGalaxyClickTrace(fallbackPath));

        return fallbackPath;
    }

    private string GetNextSystemId(
        string currentSystemId,
        string targetSystemId
    )
    {
        if (routesBuilder == null)
            return targetSystemId;

        string nextSystemId =
            routesBuilder
                .GetNextSystemIdInSelectedPath(
                    currentSystemId
                );

        return string.IsNullOrWhiteSpace(
                nextSystemId
            )
            ? targetSystemId
            : nextSystemId;
    }

    private void ShowTravelWarning(
        TravelFailReason result
    )
    {
        switch (result)
        {
            case TravelFailReason.NotEnoughFuel:
                ShowMessage(
                    "Для первого прыжка по маршруту " +
                    "недостаточно топлива"
                );
                break;

            case TravelFailReason.SystemsAreNotNeighbors:
                ShowMessage(
                    "Первая система маршрута недоступна"
                );
                break;

            case TravelFailReason.TargetSystemMissing:
                ShowMessage(
                    "Целевая система не найдена"
                );
                break;

            case TravelFailReason.CurrentSystemMissing:
                ShowMessage(
                    "Текущая система не найдена"
                );
                break;
        }
    }

    private void SetSelectedSystemVisual(
        string targetSystemId
    )
    {
        if (systemNodes == null)
            return;

        foreach (
            StarSystemNodeView2A systemNode
            in systemNodes
        )
        {
            if (systemNode == null)
                continue;

            bool selected =
                !string.IsNullOrWhiteSpace(
                    targetSystemId
                ) &&
                string.Equals(
                    systemNode.SystemId,
                    targetSystemId,
                    StringComparison.Ordinal
                );

            systemNode.SetSelected(
                selected
            );
        }
    }

    private string GetCurrentSystemId()
    {
        if (gameSessionService == null)
            return string.Empty;

        if (gameSessionService.State == null)
            return string.Empty;

        if (gameSessionService.State.Player == null)
            return string.Empty;

        return gameSessionService.State.Player
            .CurrentSystemId;
    }

    private void OnExitMapChanged(
    ExitMapChangedEvent eventData
)
    {
        galaxyMapRoot?.SetActive(false);

        invasionWarningsShownThisMapSession = 0;
    }

    public void ShowMessage(
        string text
    )
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine =
            StartCoroutine(
                ShowMessageRoutine(text)
            );
    }

    private IEnumerator ShowMessageRoutine(
        string text
    )
    {
        if (errorImage != null)
            errorImage.gameObject.SetActive(true);

        if (errorText != null)
            errorText.text = text;

        yield return new WaitForSeconds(
            errorShowDuration
        );

        if (errorImage != null)
            errorImage.gameObject.SetActive(false);

        currentRoutine = null;
    }
}
