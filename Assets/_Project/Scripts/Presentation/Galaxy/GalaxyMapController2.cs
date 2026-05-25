using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GalaxyMapController2 : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject galaxyMapRoot;

    [Header("Systems")]
    [SerializeField] private SystemNodeView2 nodePrefab2;
    [SerializeField] private Transform systemNodesContainer;
    [SerializeField] private Image errorImage;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private float showDuration = 2f;

    [Header("Camera")]
    [SerializeField] private Camera _camera;
    [SerializeField] private float _cameraSize = 1200f;
    [SerializeField] private Vector3 _cameraPosition = new Vector3(0, 0, -10f);

    private IConfigService configService;
    private IGameSessionService gameSessionService;
    private SimpleEventBus eventBus;
    private ITravelService travelService;
    private ISaveService saveService;

    private SystemNodeView2[] systemNodes;
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
        saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();

        CreateSystemNodes();

        if (_debugEnabled)
            Debug.Log("GalaxyMapController2 | systemNodeView.Count " + systemNodes.Count());

        SubscribeToEvents();
    }

    private void CreateSystemNodes()
    {
        var systems = configService.GetAllStarSystems();

        if (_debugEnabled)
            Debug.Log("GalaxyMapController2 | systemNodeView.Count " + systemNodes.Count());
        foreach (var system in systems)
        {
            if (_debugEnabled)
                Debug.Log("GalaxyMapController2 | system add " + system);

            Quaternion spawnRotation = Quaternion.identity;

            var node = Instantiate(nodePrefab2, systemNodesContainer);
            node.Initialize(system, OnSystemClicked);
            node.GetComponent<Transform>().position = 
                    new Vector2(system.MapPosition.x, system.MapPosition.y);
            
            if (_debugEnabled)
                Debug.Log("GalaxyMapController2 | MapPosition " + system.MapPosition.x +
                            " / " + system.MapPosition.y);

            if (_debugEnabled)
                Debug.Log("GalaxyMapController2 | SystemConfig " + system);

            AddNode(node);
            if (_debugEnabled)
                Debug.Log("GalaxyMapController2 | systemNodeView.Count " + systemNodes.Count());
        }
    }

    private void AddNode(SystemNodeView2 newNode)
    {
        if (systemNodes == null)
        {
            systemNodes = new SystemNodeView2[1];
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
        eventBus.Subscribe<SystemEnteredEvent>(OnSystemEntered);
        eventBus.Subscribe<ExitMapChangedEvent>(OnExitMapChanged);
    } 

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Unsubscribe<SystemEnteredEvent>(OnSystemEntered);
        eventBus.Unsubscribe<ExitMapChangedEvent>(OnExitMapChanged);
    }


    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        if (_camera != null)
        {
            _camera.orthographicSize = _cameraSize;
            _camera.transform.position = _cameraPosition;
        }
        galaxyMapRoot?.SetActive(true);
        Refresh();

        saveService.Save();
    }

    private void OnSystemEntered(SystemEnteredEvent evt)
    {
        galaxyMapRoot?.SetActive(false);
    }

    public void Refresh()
    {
        if (IsDebug())
            Debug.Log("[GalaxyMapController2] Refresh");
        foreach (SystemNodeView2 systemNodeView in systemNodes)
        {
            if (IsDebug())
                Debug.Log("[GalaxyMapController2] systemNodeView = " + systemNodeView);
            systemNodeView.SetState();   
        }
    }

    private void OnSystemClicked(string targetSystemId)
    {
        if (IsDebug())
            Debug.Log($"[GalaxyMapController2] Clicked system: {targetSystemId}");

        var currentSystemId = gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId;

        if (string.Equals(currentSystemId, targetSystemId, StringComparison.Ordinal))
        {
            if (IsDebug())
                Debug.Log($"GalaxyMapController2: '{targetSystemId}' is the current system.");
            eventBus.Publish(new SystemEnteredEvent(targetSystemId));
            return;
        }

        var result = travelService.GetTravelFailReason(currentSystemId, targetSystemId);
        if (IsDebug())
            Debug.Log($"GalaxyMapController2: travel result = {result}");                

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
        // gameSessionService.CurrentSave.PlayerProfile.CurrentStarSystemLink = systemLink;
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

        yield return new WaitForSeconds(showDuration);

        if (errorImage != null)
            errorImage.gameObject.SetActive(false);

        currentRoutine = null;
    }    
}