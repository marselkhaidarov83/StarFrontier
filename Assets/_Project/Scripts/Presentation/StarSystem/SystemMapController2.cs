using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class SystemMapController2 : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject systemMapRoot;    

    [Header("Objects")]
    [SerializeField] private GameObject systemBackground;
    [SerializeField] private Transform sunContainer;
    [SerializeField] private Transform planetContainer;
    [SerializeField] private Transform exitContainer;
    [SerializeField] private GameObject systemMapsunPrefab;
    [SerializeField] private GameObject systemMapPlanetPrefab;
    [SerializeField] private GameObject systemMapExitPrefab;

    [Header("Camera")]
    [SerializeField] private Camera _camera;
    [SerializeField] private float _cameraSize = 1200f;
    [SerializeField] private Vector3 _cameraPosition = new Vector3(0, 0, -10f);

    private SimpleEventBus eventBus;
    private IGameSessionService gameSessionService;
    private IConfigService configService;

    private readonly List<GameObject> _spawnedPlanets = new();
    private readonly List<GameObject> _spawnedExits = new();
    private GameObject _spawnedSun;

    public void Initialize()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        SubscribeToEvents();

        if (IsDebug())
            Debug.Log("[SystemMapController2] initialized");
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    public void BuildSystemMap()
    {
        ClearMap();

        if (gameSessionService == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController2] gameSessionService is null");
            return;
        }

        if (configService == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController2] configService is null");
            return;
        }

        string currentSystemId = gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId;
        if (!configService.TryGetStarSystem(currentSystemId, out StarSystemConfig starSystem))
        {
            Debug.LogError($"[SystemMapController2] No StarSystemData for id: {currentSystemId}");
            return;
        }

        SpawnSun(starSystem);
        SpawnPlanets(starSystem);
        SpawnExits(starSystem);

        if (IsDebug())
            Debug.Log($"[SystemMapController2] system map builded");
    }

    private void SpawnSun(StarSystemConfig starSystem)
    {
        if (systemMapsunPrefab == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController2] sunPrefab is null");
            return;
        }

        if (starSystem.Sun == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController2] starSystem.Sun is null");
            return;
        }

        _spawnedSun = Instantiate(systemMapsunPrefab, sunContainer);
        _spawnedSun.transform.SetSiblingIndex(0);
        _spawnedSun.GetComponent<SunNodeView>().Initialize(starSystem.Sun, null);

        if (IsDebug())
            Debug.Log($"[SystemMapController2] sun builded");
    }

    private void SpawnPlanets(StarSystemConfig starSystem)
    {
        if (systemMapPlanetPrefab == null)
        {
            Debug.LogError($"[SystemMapController2] systemMapPlanetPrefab is null");
            return;
        }

        if (starSystem.PlanetRefs == null)
        {
            Debug.LogError($"[SystemMapController2] starSystem.PlanetRefs is null");
            return;
        }

        Vector2 center = Vector2.zero;
        if (starSystem.Sun != null)
            center = starSystem.Sun.LocalOffset;

        if (IsDebug())
            Debug.Log($"[SystemMapController2] planetRefs: " + starSystem.PlanetRefs.Length);
        
        List<PlanetSelectableView> planetSelectableViews = new ();
        foreach (PlanetConfig planet in starSystem.PlanetRefs)
        {
            if (planet == null)
            {
                if (IsDebug())
                    Debug.LogError($"[SystemMapController2] planet is null");
                continue;
            }

            if (planet.PlanetOrbit == null)
            {
                if (IsDebug())
                    Debug.LogError($"[SystemMapController2] planet.PlanetOrbit is null");
                continue;
            }

            GameObject instance = Instantiate(systemMapPlanetPrefab, planetContainer);
            instance.transform.SetSiblingIndex(0);
            _spawnedPlanets.Add(instance);

            PlanetNodeView2 planetNodeView = instance.GetComponent<PlanetNodeView2>();
            // PlanetSelectableView selectableView = instance.GetComponent<PlanetSelectableView>();

            planetNodeView.Initialize(planet);

            // selectableView.Initialize(planet);
            // planetSelectableViews.Add(selectableView);
        }

        // systemMapRoot.GetComponent<SystemMapDestinationController>().SetSelectableViews(planetSelectableViews);

        if (IsDebug())
            Debug.Log($"[SystemMapController2] planets builded");
    }

    private void SpawnExits(StarSystemConfig starSystem)
    {
        if (starSystem == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController2] starSystem is null");
            return;
        }

        if (starSystem.LinkedSystems == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController2] starSystem.LinkedSystems is null");
            return;
        }

        Vector2 center = Vector2.zero;
        if (starSystem.Sun != null)
            center = starSystem.Sun.LocalOffset;

        if (IsDebug())
            Debug.Log($"[SystemMapController2] LinkedSystems.Count: " + starSystem.LinkedSystems.Length);
        
        foreach (StarSystemLink systemLink in starSystem.LinkedSystems)
        {
            if (systemLink == null)
            {
                if (IsDebug())
                    Debug.LogError($"[SystemMapController2] systemLink is null");
                continue;
            }

            GameObject instance = Instantiate(systemMapExitPrefab, exitContainer);
            instance.transform.SetSiblingIndex(0);
            _spawnedExits.Add(instance);

            if (IsDebug())
                Debug.Log("[SystemMapController2] SpawnExits.systemLink = " + systemLink.LinkedSystem.DisplayName);
            instance.GetComponent<SystemExitNodeView2>().Initialize(
                systemLink,
                center
            );
        }

        if (IsDebug())
            Debug.Log($"[SystemMapController2] exits builded");
    }

    private void ClearMap()
    {
        if (_spawnedSun != null)
            Destroy(_spawnedSun);

        foreach (var planet in _spawnedPlanets)
        {
            if (planet != null)
                Destroy(planet);
        }
        _spawnedPlanets.Clear();
        
        foreach (var exit in _spawnedExits)
        {
            if (exit != null)
                Destroy(exit);
        }
        _spawnedExits.Clear();
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Subscribe<SystemEnteredEvent>(OnSystemEntered);
        eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Subscribe<ExitMapChangedEvent>(OnExitMapChanged);

        if (IsDebug())
            Debug.Log("[SystemMapController2] events subscribed");
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Unsubscribe<SystemEnteredEvent>(OnSystemEntered);
        eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Unsubscribe<ExitMapChangedEvent>(OnExitMapChanged);

        if (IsDebug())
            Debug.Log("[SystemMapController2] events unsubscribed");
    }

    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        if (systemMapRoot != null)
            systemMapRoot.SetActive(false);

        if (IsDebug())
            Debug.Log("[SystemMapController2] galaxy entered");
    }

    private void OnSystemEntered(SystemEnteredEvent evt)
    {
        if (_camera != null)
        {
            _camera.orthographicSize = _cameraSize;
            _camera.transform.position = _cameraPosition;
        }

        BuildSystemMap();

        if (systemMapRoot != null)
            systemMapRoot.SetActive(true);

        if (IsDebug())
            Debug.Log("[SystemMapController2] system entered");
    }

    private void OnExitMapChanged(ExitMapChangedEvent evt)
    {
        if (systemMapRoot != null)
            systemMapRoot.SetActive(true);
    }


    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        if (systemMapRoot != null)
            systemMapRoot.SetActive(false);

        if (IsDebug())
            Debug.Log("[SystemMapController2] planet entered");
    }
}