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
        var registry = new ServiceRegistry();
        Services = registry;

        var eventBus = new SimpleEventBus();

        var configService = new ConfigService();
        configService.Load();

        var tickService = new TickService();
        _tickService = tickService;

        var saveValidator = new SaveValidator();
        var saveService = new SaveService(configService.SaveConfig, saveValidator);

        var gameStateService = new GameStateService();
        var gameState = saveService.Load();
        gameStateService.SetState(gameState);

        registry.Register(registry);
        registry.Register(eventBus);
        registry.Register(configService);
        registry.Register(tickService);
        registry.Register(saveService);
        registry.Register(gameStateService);

        tickService.Start();

        SceneManager.LoadScene(configService.BootstrapConfig.startGameSceneName);
    }
}