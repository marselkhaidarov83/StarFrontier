using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class SystemMapController : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject systemMapRoot;    

    [Header("Objects")]
    [SerializeField] private GameObject systemBackground;
    [SerializeField] private RectTransform sunContainer;
    [SerializeField] private RectTransform planetContainer;
    [SerializeField] private RectTransform exitContainer;
    [SerializeField] private GameObject sunPrefab;
    [SerializeField] private GameObject systemMapPlanetPrefab;
    [SerializeField] private GameObject systemMapExitPrefab;
    [SerializeField] private Button openGalaxyButton;

    private SimpleEventBus eventBus;
    private IGameSessionService gameSessionService;
    private ITravelService travelService;
    private IConfigService configService;

    private readonly List<GameObject> _spawnedPlanets = new();
    private readonly List<GameObject> _spawnedExits = new();
    private GameObject _spawnedSun;

    public void Initialize()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        travelService = Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();
        configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        // BuildSystemMap();
        SubscribeToEvents();
        BindButtons();

        if (IsDebug())
            Debug.Log("[SystemMapController] initialized");
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        UnbindButtons();
    }

    public void BuildSystemMap()
    {
        ClearMap();

        if (gameSessionService == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] gameSessionService is null");
            return;
        }

        if (configService == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] configService is null");
            return;
        }

        string currentSystemId = gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId;
        if (!configService.TryGetStarSystem(currentSystemId, out StarSystemConfig starSystem))
        {
            Debug.LogError($"[SystemMapController] No StarSystemData for id: {currentSystemId}");
            return;
        }

        SpawnSun(starSystem);
        SpawnPlanets(starSystem);
        SpawnExits(starSystem);

        if (IsDebug())
            Debug.Log($"[SystemMapController] system map builded");
    }

    private void SpawnSun(StarSystemConfig starSystem)
    {
        if (sunPrefab == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] sunPrefab is null");
            return;
        }

        if (starSystem.Sun == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] starSystem.Sun is null");
            return;
        }

        _spawnedSun = Instantiate(sunPrefab, sunContainer);
        _spawnedSun.transform.SetSiblingIndex(0);
        RectTransform rect = _spawnedSun.GetComponent<RectTransform>();
        Image image = _spawnedSun.GetComponent<Image>();

        rect.anchoredPosition = starSystem.Sun.LocalOffset;
        rect.sizeDelta = new Vector2(starSystem.Sun.VisualSize, starSystem.Sun.VisualSize);

        if (image != null)
            image.sprite = starSystem.Sun.SunSprite;

        if (IsDebug())
            Debug.Log($"[SystemMapController] sun builded");
    }

    private void SpawnPlanets(StarSystemConfig starSystem)
    {
        if (systemMapPlanetPrefab == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] systemMapPlanetPrefab is null");
            return;
        }

        if (starSystem.PlanetRefs == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] starSystem.PlanetRefs is null");
            return;
        }

        Vector2 center = Vector2.zero;
        if (starSystem.Sun != null)
            center = starSystem.Sun.LocalOffset;

        if (IsDebug())
            Debug.Log($"[SystemMapController] planetRefs: " + starSystem.PlanetRefs.Length);
        
        List<PlanetSelectableView> planetSelectableViews = new ();
        foreach (PlanetConfig planet in starSystem.PlanetRefs)
        {
            // if (orbitData == null || orbitData.Planet == null)
            if (planet == null)
            {
                if (IsDebug())
                    Debug.LogError($"[SystemMapController] planet is null");
                continue;
            }

            if (planet.PlanetOrbit == null)
            {
                if (IsDebug())
                    Debug.LogError($"[SystemMapController] planet.PlanetOrbit is null");
                continue;
            }

            GameObject instance = Instantiate(systemMapPlanetPrefab, planetContainer);
            instance.transform.SetSiblingIndex(0);
            _spawnedPlanets.Add(instance);

            PlanetNodeView view = instance.GetComponent<PlanetNodeView>();
            PlanetSelectableView selectableView = instance.GetComponent<PlanetSelectableView>();

            view.Initialize(
                planet,
                OnPlanetClicked,
                center
            );

            selectableView.Initialize(planet);
            planetSelectableViews.Add(selectableView);
        }

        systemMapRoot.GetComponent<SystemMapDestinationController>().SetSelectableViews(planetSelectableViews);
        // systemMapRoot.GetComponent<SystemMapDestinationController>().RebindPlanetViews(planetSelectableViews);

        if (IsDebug())
            Debug.Log($"[SystemMapController] planets builded");
    }

    private void SpawnExits(StarSystemConfig starSystem)
    {
        if (starSystem == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] starSystem is null");
            return;
        }

        if (starSystem.LinkedSystems == null)
        {
            if (IsDebug())
                Debug.LogError($"[SystemMapController] starSystem.LinkedSystems is null");
            return;
        }

        Vector2 center = Vector2.zero;
        if (starSystem.Sun != null)
            center = starSystem.Sun.LocalOffset;

        if (IsDebug())
            Debug.Log($"[SystemMapController] LinkedSystems.Count: " + starSystem.LinkedSystems.Length);
        
        foreach (StarSystemLink systemLink in starSystem.LinkedSystems)
        {
            if (systemLink == null)
            {
                if (IsDebug())
                    Debug.LogError($"[SystemMapController] systemLink is null");
                continue;
            }

            GameObject instance = Instantiate(systemMapExitPrefab, exitContainer);
            instance.transform.SetSiblingIndex(0);
            _spawnedExits.Add(instance);

            SystemExitNodeView view = instance.GetComponent<SystemExitNodeView>();

            view.Initialize(
                systemLink,
                center
            );
        }

        if (IsDebug())
            Debug.Log($"[SystemMapController] exits builded");
    }

    private void OnPlanetClicked(string planetId)
    {
        travelService.TryTravelToPlanet(planetId);
        // gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId = planetId;

        // if (eventBus != null)
        //     eventBus.Publish(new PlanetEnteredEvent(planetId));

        Debug.Log($"[SystemMapController] Planet clicked: {planetId}");
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
            Debug.Log("[SystemMapController] events subscribed");
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
            Debug.Log("[SystemMapController] events unsubscribed");
    }

    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        if (systemMapRoot != null)
            systemMapRoot.SetActive(false);

        if (IsDebug())
            Debug.Log("[SystemMapController] galaxy entered");
    }

    private void OnSystemEntered(SystemEnteredEvent evt)
    {
        BuildSystemMap();

        if (systemMapRoot != null)
            systemMapRoot.SetActive(true);

        if (IsDebug())
            Debug.Log("[SystemMapController] system entered");
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
            Debug.Log("[SystemMapController] planet entered");
    }

    private void BindButtons()
    {
        if (openGalaxyButton != null)
            openGalaxyButton.onClick.AddListener(OnOpenGalaxyClicked);
    }

    private void UnbindButtons()
    {
        if (openGalaxyButton != null)
            openGalaxyButton.onClick.RemoveListener(OnOpenGalaxyClicked);
    }    

    private void OnOpenGalaxyClicked()
    {
        if (eventBus != null)
            eventBus.Publish(new GalaxyEnteredEvent());
    }
}