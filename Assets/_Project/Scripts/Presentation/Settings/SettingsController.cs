using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private Button _analyticsButton;
    private TMP_Text _analyticsLabel;

    private void Start()
    {
        CreateAnalyticsSetting();
    }

    private void CreateAnalyticsSetting()
    {
        var analytics = PlayerAnalyticsSafe.Get();
        if (analytics == null) return;
        Canvas canvas = null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var candidate = root.GetComponentInChildren<Canvas>();
            if (candidate != null && candidate.transform.Find("MainMenuPanel/BackButton") != null)
            { canvas = candidate; break; }
        }
        if (canvas == null) return;
        var template = canvas.transform.Find("MainMenuPanel/BackButton")?.GetComponent<Button>();
        if (template == null || template.transform.parent.Find("AnalyticsButton") != null) return;

        _analyticsButton = Instantiate(template, template.transform.parent);
        _analyticsButton.name = "AnalyticsButton";
        var rect = (RectTransform)_analyticsButton.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 120f);
        rect.sizeDelta = new Vector2(900f, 150f);
        _analyticsButton.onClick = new Button.ButtonClickedEvent();
        _analyticsButton.onClick.AddListener(ToggleAnalytics);
        _analyticsLabel = _analyticsButton.GetComponentInChildren<TMP_Text>();
        if (_analyticsLabel != null)
        {
            _analyticsLabel.enableAutoSizing = true;
            _analyticsLabel.fontSizeMin = 24f;
            _analyticsLabel.fontSizeMax = 52f;
        }

        var noticeObject = new GameObject("AnalyticsNotice", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        noticeObject.transform.SetParent(template.transform.parent, false);
        var noticeRect = (RectTransform)noticeObject.transform;
        noticeRect.anchorMin = noticeRect.anchorMax = new Vector2(0.5f, 0.5f);
        noticeRect.anchoredPosition = new Vector2(0f, -140f);
        noticeRect.sizeDelta = new Vector2(900f, 300f);
        var notice = noticeObject.GetComponent<TextMeshProUGUI>();
        if (_analyticsLabel != null) notice.font = _analyticsLabel.font;
        notice.text = "Статистика действий помогает улучшать прохождение и баланс. " +
            "В AppMetrica передаются события игры, версия приложения и сведения об устройстве. " +
            "Отправку можно выключить здесь.";
        notice.fontSize = 38f;
        notice.alignment = TextAlignmentOptions.Center;
        notice.raycastTarget = false;
        RefreshAnalyticsLabel();
    }

    public void ToggleAnalytics()
    {
        var analytics = PlayerAnalyticsSafe.Get();
        if (analytics == null) return;
        analytics.SetCollectionEnabled(!analytics.CollectionEnabled);
        RefreshAnalyticsLabel();
    }

    private void RefreshAnalyticsLabel()
    {
        if (_analyticsLabel != null)
            _analyticsLabel.text = PlayerAnalyticsSafe.Get()?.CollectionEnabled == true
                ? "Статистика игры: ВКЛ" : "Статистика игры: ВЫКЛ";
    }

    public void OnBackClicked()
    {
        if (_debugEnabled)
            AppLog.Info(DebugLogChannel.Settings, "Back clicked");
    }
}