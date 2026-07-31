using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameBootstrapper : MonoBehaviour
{
    private static GameBootstrapper _instance;

    public static ServiceRegistry Services { get; private set; }

    private TickService _tickService;

    private void Awake()
    {
        if (_instance != null)
        {
            Debug.LogWarning("GameBootstrapper: duplicate bootstrapper found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        Bootstrap();
    }

    private void Update()
    {
        _tickService?.Tick(Time.deltaTime);
    }

    private void Bootstrap()
    {
        Debug.Log("GameBootstrapper: bootstrap started.");

        var serviceRegistry = new ServiceRegistry();
        Services = serviceRegistry;

        var eventBus = new SimpleEventBus();

        var configService = new ConfigServiceU();
        configService.Load();

        if (!configService.IsLoaded)
        {
            Debug.LogError("GameBootstrapper: ConfigService failed to load configs. Bootstrap stopped.");
            return;
        }

        var tickService = new TickService();
        _tickService = tickService;

        var saveValidator = new SaveValidator(
            defaultSaveVersion: configService.SaveConfig.saveVersion,
            defaultPlayerName: configService.BootstrapConfig.defaultPlayerName,
            defaultSystemId: configService.BootstrapConfig.defaultSystemId
        );

        var saveService = new SaveServiceU(
            config: configService.SaveConfig,
            validator: saveValidator
        );

        GameState loadedState = saveService.Load();

        var gameStateService = new GameStateService();
        gameStateService.SetState(loadedState);

        // GalaxyGenerationConfig galaxyConfig = configService.GetGalaxyGenerationConfig();
        // GalaxyState galaxyState = saveService.CurrentState.Galaxy;
        // GalaxySimulationService galaxySimulationService = new GalaxySimulationService(
        //     galaxyConfig,
        //     galaxyState,
        //     eventBus);

        serviceRegistry.Register(serviceRegistry);
        serviceRegistry.Register(eventBus);
        serviceRegistry.Register(configService);
        serviceRegistry.Register(tickService);
        serviceRegistry.Register(saveService);
        serviceRegistry.Register(gameStateService);
        // serviceRegistry.Register(galaxySimulationService);

        // galaxySimulationService.EnsureGenerated();

        tickService.Start();

        Debug.Log("GameBootstrapper: services registered successfully.");
        Debug.Log($"GameBootstrapper: current system = {gameStateService.State.player.currentSystemId}");
        Debug.Log($"GameBootstrapper: total ticks = {gameStateService.State.meta.totalTicks}");

        LoadStartScene(configService);
    }

    private void LoadStartScene(ConfigServiceU configService)
    {
        string sceneName = configService.BootstrapConfig.startGameSceneName;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("GameBootstrapper: start scene name is empty.");
            return;
        }

        Debug.Log($"GameBootstrapper: loading scene {sceneName}.");
        SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        if (_instance != this)
        {
            return;
        }

        _tickService?.Stop();
        Services?.Clear();

        Services = null;
        _instance = null;
    }
}