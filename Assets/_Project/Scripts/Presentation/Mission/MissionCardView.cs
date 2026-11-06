using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionCardView : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image missionTypeIcon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text shortDescriptionText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text stateBadgeText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button doneButton;

    private MissionInstanceData _missionData;
    private Action<MissionInstanceData> _onSelected;
    private Action<MissionInstanceData> _onAccept;
    private Action<MissionInstanceData> _onDone;
    private Action<MissionInstanceData> _onFly;
    private Image _topTrim;
    private Image _leftTrim;
    private Outline _cardOutline;
    private float _lastBodyWidth = -1f;

    private void LateUpdate()
    {
        float width = ((RectTransform)transform).rect.width;
        if (width > 0f && Mathf.Abs(width - _lastBodyWidth) > 0.5f)
            LayoutMissionBody(width);
    }

    public void Bind(MissionInstanceData missionData, Sprite missionIcon, IConfigService config,
        Action<MissionInstanceData> onSelected, Action<MissionInstanceData> onAccept,
        Action<MissionInstanceData> onDone, bool isSelected,
        Action<MissionInstanceData> onFly = null)
    {
        _missionData = missionData;
        _onSelected = onSelected;
        _onAccept = onAccept;
        _onDone = onDone;
        _onFly = onFly;
        ApplyMobileLayout();

        if (missionTypeIcon != null)
        {
            missionTypeIcon.sprite = missionIcon;
            missionTypeIcon.enabled = missionIcon != null;
        }
        if (titleText != null) titleText.text = MissionUiText.Title(missionData, config);
        if (shortDescriptionText != null)
        {
            string description = MissionUiText.Description(missionData, config);
            int day = CurrentMissionDay();
            string deadline = MissionUiText.Deadline(missionData, day);
            shortDescriptionText.text = description +
                (string.IsNullOrWhiteSpace(deadline) ? string.Empty : "\n" + deadline);
        }
        if (rewardText != null) rewardText.text = MissionUiText.Reward(missionData);
        PlayerState player = Bootstrapper.Instance?.ServiceRegistry?.Get<IGameSessionService>()?.State?.Player;
        bool rewardHere = CanTurnInHere(missionData, player, config);
        if (progressText != null)
            progressText.text = rewardHere
                ? MissionUiText.RewardStepOnPlanet(missionData, config)
                : MissionUiText.NextStep(missionData, config, player);
        LayoutMissionBody(((RectTransform)transform).rect.width);
        if (statusText != null) statusText.gameObject.SetActive(false);

        bool offered = missionData != null && missionData.Status == MissionStatus.Available;
        bool ready = missionData != null && missionData.Status == MissionStatus.ReadyToComplete;
        bool inProgress = missionData != null &&
            (missionData.Status == MissionStatus.Accepted || missionData.Status == MissionStatus.InProgress);
        bool canFly = inProgress && (missionData.MissionType == MissionType.Delivery ||
            missionData.MissionType == MissionType.Recon ||
            missionData.MissionType == MissionType.RouteSearch);
        if (stateBadgeText != null) stateBadgeText.gameObject.SetActive(false);
        if (selectedFrame != null) selectedFrame.SetActive(isSelected);
        if (backgroundImage != null)
        {
            backgroundImage.enabled = true;
            backgroundImage.color = ready
                ? new Color(0.15f, 0.24f, 0.31f, 1f)
                : MissionPanelStyle2A.CardBackground;
        }
        Color accent = ready
            ? new Color(0.95f, 0.68f, 0.23f, 1f)
            : MissionPanelStyle2A.CardAccent;
        if (_cardOutline != null) _cardOutline.effectColor = accent;
        if (_topTrim != null) _topTrim.color = accent;
        if (_leftTrim != null) _leftTrim.color = accent;

        if (doneButton != null)
        {
            doneButton.gameObject.SetActive(offered || ready || inProgress);
            doneButton.interactable = offered || canFly || rewardHere;
            TMP_Text buttonText = doneButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null)
            {
                buttonText.text = offered ? "Принять миссию" :
                    ready ? "Забрать награду" : canFly ? "Лететь" : "Выполняется";
                buttonText.fontSize = 24f;
                buttonText.fontStyle = FontStyles.Normal;
                buttonText.color = Color.white;
                buttonText.alignment = TextAlignmentOptions.Center;
            }
            StyleActionButton(ready, inProgress && !canFly);
            doneButton.onClick.RemoveAllListeners();
            if (offered || ready || canFly) doneButton.onClick.AddListener(() =>
            {
                if (offered) _onAccept?.Invoke(_missionData);
                else if (ready) _onDone?.Invoke(_missionData);
                else _onFly?.Invoke(_missionData);
            });
        }
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => _onSelected?.Invoke(_missionData));
        }
    }

    public static bool CanTurnInHere(MissionInstanceData mission, PlayerState player,
        IConfigService config)
    {
        if (mission == null || player == null || mission.Status != MissionStatus.ReadyToComplete)
            return false;
        if (mission.MissionType == MissionType.Delivery)
            return mission.TargetSystemId == player.CurrentSystemId &&
                   mission.TargetPlanetId == player.CurrentPlanetId;
        if (mission.MissionType == MissionType.Recon || mission.MissionType == MissionType.RouteSearch)
            return mission.SourceSystemId == player.CurrentSystemId &&
                   mission.SourcePlanetId == player.CurrentPlanetId;
        if (MissionBattleRules.IsBattle(mission.MissionType))
            return mission.TargetSystemId == player.CurrentSystemId &&
                   config?.GetPlanetConfigById(player.CurrentPlanetId)?.IsInhabited == true;
        return false;
    }

    private void ApplyMobileLayout()
    {
        EnsureCardFrame();
        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout != null) layout.preferredHeight = 280f;
        LayoutText(titleText, 18f, 64f, 100f, 0.98f);
        if (titleText != null)
        {
            titleText.fontSize = 34f;
            titleText.alignment = TextAlignmentOptions.Left;
        }
        foreach (TMP_Text bodyText in new[] { shortDescriptionText, rewardText, progressText })
        {
            if (bodyText == null) continue;
            bodyText.fontSize = 25f;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (stateBadgeText != null)
        {
            RectTransform rect = stateBadgeText.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-22f, 20f);
            rect.sizeDelta = new Vector2(260f, 66f);
            stateBadgeText.alignment = TextAlignmentOptions.Center;
            stateBadgeText.textWrappingMode = TextWrappingModes.Normal;
            stateBadgeText.fontSize = 23f;
        }

        if (doneButton != null)
        {
            RectTransform rect = doneButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-22f, 20f);
            rect.sizeDelta = new Vector2(260f, 66f);
        }
        if (missionTypeIcon != null)
        {
            RectTransform rect = missionTypeIcon.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -18f);
            rect.sizeDelta = new Vector2(64f, 64f);
        }
    }

    private void LayoutMissionBody(float width)
    {
        if (width <= 0f || shortDescriptionText == null || rewardText == null || progressText == null)
            return;
        _lastBodyWidth = width;
        float descriptionHeight = PreferredHeight(shortDescriptionText, width * 0.96f - 28f);
        float rewardHeight = PreferredHeight(rewardText, width * 0.72f - 28f);
        float stepHeight = PreferredHeight(progressText, width * 0.72f - 28f);
        const float descriptionTop = 94f;
        float rewardTop = descriptionTop + descriptionHeight + 6f;
        float stepTop = rewardTop + rewardHeight + 5f;
        LayoutText(shortDescriptionText, descriptionTop, descriptionHeight, 28f, 0.96f);
        LayoutText(rewardText, rewardTop, rewardHeight, 28f, 0.72f);
        LayoutText(progressText, stepTop, stepHeight, 28f, 0.72f);
        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout != null) layout.preferredHeight = Mathf.Max(280f, stepTop + stepHeight + 18f);
    }

    private static float PreferredHeight(TMP_Text text, float width) =>
        Mathf.Max(40f, text.GetPreferredValues(text.text, Mathf.Max(100f, width), Mathf.Infinity).y + 8f);

    private static void LayoutText(TMP_Text text, float top, float height, float left, float widthFraction)
    {
        if (text == null) return;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(widthFraction, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(-left, height);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.TopLeft;
    }

    private void EnsureCardFrame()
    {
        if (backgroundImage == null) return;
        _cardOutline = backgroundImage.GetComponent<Outline>();
        if (_cardOutline == null) _cardOutline = backgroundImage.gameObject.AddComponent<Outline>();
        _cardOutline.effectDistance = new Vector2(2f, 2f);
        _cardOutline.useGraphicAlpha = false;
        _topTrim = CreateTrim("MissionCardTopTrim", new Vector2(0f, 1f),
            new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(-4f, 4f));
        _leftTrim = CreateTrim("MissionCardLeftTrim", Vector2.zero,
            new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(5f, -4f));
    }

    private Image CreateTrim(string name, Vector2 min, Vector2 max, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        Transform existing = transform.Find(name);
        if (existing != null) return existing.GetComponent<Image>();
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.SetAsFirstSibling();
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void StyleActionButton(bool ready, bool inProgress)
    {
        if (doneButton == null) return;
        Image image = doneButton.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = ready
                ? new Color(0.48f, 0.31f, 0.08f, 1f)
                : inProgress ? MissionPanelStyle2A.DisabledBackground
                : MissionPanelStyle2A.ActionBackground;
            Outline outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = ready
                ? new Color(1f, 0.73f, 0.25f, 1f)
                : inProgress ? MissionPanelStyle2A.DisabledAccent
                : MissionPanelStyle2A.ActionAccent;
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = false;
        }
        ColorBlock colors = doneButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.7f, 0.85f, 0.95f, 1f);
        colors.disabledColor = inProgress ? Color.white :
            new Color(0.52f, 0.58f, 0.63f, 0.8f);
        doneButton.colors = colors;
    }

    private static int CurrentMissionDay()
    {
        IServiceRegistry services = Bootstrapper.Instance?.ServiceRegistry;
        if (services == null) return 1;
        if (services.TryGet<IGameTimeService>(out IGameTimeService time))
            return Math.Max(1, time.CurrentQuantTick);
        return Math.Max(1, services.Get<IGameSessionService>()?.State?.Meta?.CurrentGameDay ?? 1);
    }
}
