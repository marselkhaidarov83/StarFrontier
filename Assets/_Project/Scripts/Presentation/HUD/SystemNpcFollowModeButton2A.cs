using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SystemNpcFollowModeButton2A :
    CustomMonoBehaviour,
    ISystemHudWidgetBinder2A
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text modeText;

    [Header("Sprites")]
    [SerializeField] private Sprite closeModeSprite;
    [SerializeField] private Sprite middleModeSprite;
    [SerializeField] private Sprite farModeSprite;

    private SimpleEventBus _eventBus;
    private ISystemTravelService _travelService;
    private bool _isBound;
    private bool _hasNpcOrEnemyTarget;

    public bool IsBound => _isBound;

    private void Awake()
    {
        ResolveReferences();
        RefreshVisual();
    }

    private void OnEnable()
    {
        BindButton();
        RefreshVisual();
    }

    private void OnDisable()
    {
        UnbindButton();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void Bind(SystemHudBindingContext2A context)
    {
        if (context == null)
            return;

        _eventBus = context.Get<SimpleEventBus>();
        context.TryGet(out _travelService);

        _eventBus.Subscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(
            OnSelectedTargetPanelRequested);
        _eventBus.Subscribe<SystemSelectedTargetInfoPanelCloseRequestedEvent2A>(
            OnSelectedTargetInfoPanelCloseRequested);
        _eventBus.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(
            OnObjectsPanelCloseRequested);
        _eventBus.Subscribe<SystemObjectsPanelRequestedEvent2A>(
            OnObjectsPanelRequested);
        _eventBus.Subscribe<SystemNpcFollowModeChangedEvent2A>(
            OnNpcFollowModeChanged);

        _isBound = true;

        BindButton();
        RefreshVisible();
        RefreshVisual();
    }

    public void Unbind()
    {
        UnbindButton();

        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(
                OnSelectedTargetPanelRequested);
            _eventBus.Unsubscribe<SystemSelectedTargetInfoPanelCloseRequestedEvent2A>(
                OnSelectedTargetInfoPanelCloseRequested);
            _eventBus.Unsubscribe<SystemObjectsPanelCloseRequestedEvent2A>(
                OnObjectsPanelCloseRequested);
            _eventBus.Unsubscribe<SystemObjectsPanelRequestedEvent2A>(
                OnObjectsPanelRequested);
            _eventBus.Unsubscribe<SystemNpcFollowModeChangedEvent2A>(
                OnNpcFollowModeChanged);
        }

        _eventBus = null;
        _travelService = null;
        _isBound = false;
        _hasNpcOrEnemyTarget = false;
    }

    private void BindButton()
    {
        ResolveReferences();

        if (button == null)
            return;

        button.onClick.RemoveListener(OnClicked);
        button.onClick.AddListener(OnClicked);
    }

    private void UnbindButton()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    private void OnClicked()
    {
        _travelService?.CycleNpcFollowMode();
        RefreshVisual();
    }

    private void OnSelectedTargetPanelRequested(
        SystemSelectedTargetInfoPanelRequestedEvent2A evt)
    {
        _hasNpcOrEnemyTarget =
            evt.TargetType == SystemGameplayTargetType.Ally ||
            evt.TargetType == SystemGameplayTargetType.Enemy;

        RefreshVisible();
        RefreshVisual();
    }

    private void OnSelectedTargetInfoPanelCloseRequested(
        SystemSelectedTargetInfoPanelCloseRequestedEvent2A evt)
    {
        _hasNpcOrEnemyTarget = false;
        RefreshVisible();
    }

    private void OnObjectsPanelCloseRequested(
        SystemObjectsPanelCloseRequestedEvent2A evt)
    {
        _hasNpcOrEnemyTarget = false;
        RefreshVisible();
    }

    private void OnObjectsPanelRequested(
        SystemObjectsPanelRequestedEvent2A evt)
    {
        _hasNpcOrEnemyTarget = false;
        RefreshVisible();
    }

    private void OnNpcFollowModeChanged(
        SystemNpcFollowModeChangedEvent2A evt)
    {
        RefreshVisual();
    }

    private void RefreshVisible()
    {
        if (!_isBound)
            return;

        gameObject.SetActive(_hasNpcOrEnemyTarget);
    }

    private void RefreshVisual()
    {
        int modeIndex =
            _travelService != null
                ? _travelService.NpcFollowModeIndex
                : 0;

        Sprite modeSprite =
            GetSprite(modeIndex);

        if (iconImage != null && modeSprite != null)
            iconImage.sprite = modeSprite;

        if (modeText != null)
            modeText.SetText((modeIndex + 1).ToString());
    }

    private Sprite GetSprite(int modeIndex)
    {
        switch (modeIndex)
        {
            case 0:
                return closeModeSprite;

            case 1:
                return middleModeSprite;

            case 2:
                return farModeSprite;

            default:
                return closeModeSprite;
        }
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponent<Image>();

        if (modeText == null)
            modeText = GetComponentInChildren<TMP_Text>(true);
    }
}