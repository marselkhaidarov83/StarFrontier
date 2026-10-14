using UnityEngine;
using UnityEngine.UI;

public class SystemMapHUDController : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject systemMapHUDRoot;    

    [Header("Objects")]
    [SerializeField] private Button openGalaxyButton;

    private SimpleEventBus eventBus;
    private IGameStateMachine gameStateMachine;

    public void Initialize()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        SubscribeToEvents();
        BindButtons();

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "initialized");
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        UnbindButtons();
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Subscribe<StarSystemEnteredEvent>(OnSystemEntered);
        eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Subscribe<ExitMapChangedEvent>(OnExitMapChanged);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "events subscribed");
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Unsubscribe<StarSystemEnteredEvent>(OnSystemEntered);
        eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Unsubscribe<ExitMapChangedEvent>(OnExitMapChanged);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "events unsubscribed");
    }

    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(false);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "galaxy entered");
    }

    private void OnSystemEntered(StarSystemEnteredEvent evt)
    {
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(true);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "system entered");
    }

    private void OnExitMapChanged(ExitMapChangedEvent evt)
    {
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(true);
    }


    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(false);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "planet entered");
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
        gameStateMachine.Enter(new GalaxyState());
        // if (eventBus != null)
            // eventBus?.Publish(new GalaxyEnteredEvent());
    }
}