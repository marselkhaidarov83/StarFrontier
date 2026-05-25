using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{    
    [SerializeField] private Button _continueButton;
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
                Debug.LogError("Bootstrapper.Instance is null.");
            return;
        }

        _newGameService = Bootstrapper.Instance.ServiceRegistry.Get<INewGameService>();
        _continueGameService = Bootstrapper.Instance.ServiceRegistry.Get<IContinueGameService>();
        _marketTransactionService = Bootstrapper.Instance.ServiceRegistry.Get<IMarketTransactionService>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        if (_newGameService == null)
            if (_debugEnabled)
                Debug.LogError("NewGameService not found in ServiceRegistry.");

        if (_continueGameService == null)
            if (_debugEnabled)
                Debug.LogError("ContinueGameService not found in ServiceRegistry.");
    }

    private void Start()
    {
        RefreshContinueButton();
    }

    public void OnNewGameClicked()
    {
        if (_newGameService == null)
        {
            Debug.LogError("NewGameService is not initialized.");
            return;
        }

        _newGameService.StartNewGame();

        // var _economyService = Bootstrapper.Instance.ServiceRegistry.Get<IEconomyService>();
        // EconomyDebugTester.PrintCurrentMarket(_economyService);

        // MarketDebugTester.TestBuyItem(
        //     _marketTransactionService,
        //     gameSessionService,
        //     "item_ore_01",
        //     10);

        // MarketDebugTester.TestSellItem(
        //     _marketTransactionService,
        //     gameSessionService,
        //     "item_food_01",
        //     0);        
    }
    
    public void OnContinueClicked()
    {
        if (_continueGameService == null)
        {
            if (_debugEnabled)
                Debug.LogError("ContinueGameService is not initialized.");
            return;
        }

        bool success = _continueGameService.ContinueGame();

        if (!success)
        {
            if (_debugEnabled)
                Debug.LogWarning("Continue game failed.");
            RefreshContinueButton();
        }
    }

    public void RefreshContinueButton()
    {
        if (_continueButton == null || _continueGameService == null)
            return;

        _continueButton.interactable = _continueGameService.CanContinue();
    }

    public void OnSettingsClicked()
    {
        if (_debugEnabled)
            Debug.Log("Settings clicked");
    }

    public void OnExitClicked()
    {
        if (_debugEnabled)
            Debug.Log("Exit clicked");

#if UNITY_EDITOR
        if (_debugEnabled)
            Debug.Log("Exit works only in build");
#else
        Application.Quit();
#endif
    }
}