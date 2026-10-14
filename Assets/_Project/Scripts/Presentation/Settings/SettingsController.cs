using UnityEngine;

public class SettingsController : MonoBehaviour
{    
    [SerializeField] private bool _debugEnabled;

    private INewGameService _newGameService;
    private IContinueGameService _continueGameService;
    private IMarketTransactionService _marketTransactionService;
    private IGameSessionService gameSessionService;

    private void Awake()
    {
        if (Bootstrapper.Instance == null)
        {
            if (_debugEnabled)
                AppLog.Error(DebugLogChannel.Settings, "Bootstrapper.Instance is null.");
            return;
        }

        _newGameService = Bootstrapper.Instance.ServiceRegistry.Get<INewGameService>();
        _continueGameService = Bootstrapper.Instance.ServiceRegistry.Get<IContinueGameService>();
        _marketTransactionService = Bootstrapper.Instance.ServiceRegistry.Get<IMarketTransactionService>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        if (_newGameService == null)
            if (_debugEnabled)
                AppLog.Error(DebugLogChannel.Settings, "NewGameService not found in ServiceRegistry.");

        if (_continueGameService == null)
            if (_debugEnabled)
                AppLog.Error(DebugLogChannel.Settings, "ContinueGameService not found in ServiceRegistry.");
    }

    private void Start()
    {
        
    }

    public void OnBackClicked()
    {
        if (_debugEnabled)
            AppLog.Info(DebugLogChannel.Settings, "Back clicked");
    }
}