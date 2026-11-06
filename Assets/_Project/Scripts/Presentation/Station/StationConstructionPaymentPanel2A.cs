using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Ordinary (credits + astralite) and accelerated (diamonds) construction, with one guarded action.</summary>
public sealed class StationConstructionPaymentPanel2A : MonoBehaviour
{
    private sealed class PaymentGroup
    {
        public Button Button;
        public Image Header;
        public Outline Outline;
        public TMP_Text Title, Time;
    }
    private sealed class ResourceCard
    {
        public RectTransform Rect;
        public TMP_Text Label, Amount;
        public Image Icon;
        public StationPaymentResource Resource;
        public int Group;
    }
    private static readonly Color IdleFill = new Color32(12, 20, 39, 245);
    private static readonly Color SelectedFill = new Color32(18, 51, 90, 250);
    private static readonly Color DisabledFill = new Color32(65, 73, 90, 255);
    private static readonly Color ShortageColor = new Color32(255, 70, 70, 255);
    private Transform _template;
    private IStationCardService _cards;
    private ResearchPaymentCardService2A _researchSource;
    private IStationCardService Source => _researchSource != null ? (IStationCardService)_researchSource : _cards;
    private IGameSessionService _session;
    private IGameTimeService _time;
    private Func<StationPaymentResource, Sprite> _paymentIcon;
    private Action<string> _completed;
    private GameObject _overlay;
    private RectTransform _canvas, _form;
    private TMP_Text _style, _notice, _header, _confirmText;
    private Image _headerIcon;
    private Color _idleHeader;
    private Button _confirm;
    private readonly PaymentGroup[] _groups = new PaymentGroup[2];
    private readonly ResourceCard[] _resources = new ResourceCard[3];
    private readonly StationActionOffer[] _offers = new StationActionOffer[2];
    private StationActionConfirmation _ticket;
    private GameRuntimeState _openedSession;
    private string _systemId, _stationId;
    private int _selected = -1, _openedLevel;
    private StationOperationKind _operationKind;
    private bool _open, _savedPause, _busy;
    private float _nextRefresh;
    public bool IsOpen => _open;

    public void Initialize(Transform template, IStationCardService cards, IGameSessionService session,
        IGameTimeService time, Func<string, string> itemName, Action<string> completed,
        Func<StationPaymentResource, Sprite> paymentIcon = null)
    {
        _template = template; _cards = cards; _session = session; _time = time;
        _completed = completed; _paymentIcon = paymentIcon;
    }
    public bool Open(string systemId) => OpenStationOperation(systemId, StationOperationKind.Construction);
    public bool OpenUpgrade(string systemId) => OpenStationOperation(systemId, StationOperationKind.Upgrade);
    public bool OpenRecovery(string systemId) => OpenStationOperation(systemId, StationOperationKind.Recovery);
    public bool OpenResearch(string systemId, ResearchPaymentCardService2A source)
    {
        if (_open || source == null) return false;
        _researchSource = source;
        bool opened = OpenOperation(systemId, StationOperationKind.Upgrade);
        if (!opened) { source.CancelConfirmation(); _researchSource = null; }
        return opened;
    }
    private bool OpenStationOperation(string systemId, StationOperationKind kind)
    {
        if (_open && _researchSource != null) return false;
        if (!_open) _researchSource = null;
        return OpenOperation(systemId, kind);
    }
    private bool CanOpenCurrent(StationCardSnapshot card, StationOperationKind kind) =>
        _researchSource != null ? _researchSource.CanOpen(card) : CanOpen(card, kind);
    private static bool CanOpen(StationCardSnapshot card, StationOperationKind kind)
    {
        var station = card?.Station;
        if (station == null || card.IsCaptured || station.Recovery != null) return false;
        // A destroyed station may retain an already paid upgrade; recovery must not discard it.
        if (kind == StationOperationKind.Recovery) return station.IsDestroyed && station.Level >= 1 && station.Level <= StationLifecycleRules.MaxLevel;
        if (station.IsDestroyed || station.Development != null) return false;
        return kind == StationOperationKind.Construction ? station.Level == 0 :
            kind == StationOperationKind.Upgrade && station.Level >= 1 && station.Level < 10;
    }
    private bool OpenOperation(string systemId, StationOperationKind kind)
    {
        if (_open) return _operationKind == kind && _systemId == systemId;
        var card = Source?.GetCard(systemId);
        if (!CanOpenCurrent(card, kind) || _time == null || !EnsureUi()) return false;
        _operationKind = kind; _openedLevel = card.Station.Level;
        _systemId = systemId; _stationId = card.Station.ConfigId; _openedSession = _session.State;
        _savedPause = _time.IsPaused; _open = true; _selected = -1;
        Source.CancelConfirmation(); _ticket = null;
        _overlay.SetActive(true); _overlay.transform.SetAsLastSibling();
        _time.SetPaused(true); Refresh(); SelectOption(0); Log("PAYMENT_OPENED"); return true;
    }
    public void Close()
    {
        if (!_open) return;
        _open = false; _selected = -1; _ticket = null; Source.CancelConfirmation();
        if (_overlay != null) _overlay.SetActive(false);
        if (ReferenceEquals(_openedSession, _session.State)) _time.SetPaused(_savedPause);
        _openedSession = null; _researchSource = null; Log("PAYMENT_CLOSED");
    }
    private void OnDisable() => Close();
    private void OnDestroy()
    {
        Close();
        if (_overlay != null)
        {
            if (Application.isPlaying) Destroy(_overlay); else DestroyImmediate(_overlay);
        }
    }
    private void Update()
    {
        if (!_open || _busy) return;
        if (!_time.IsPaused) _time.SetPaused(true);
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .15f; Refresh();
    }
    private static bool UsesDiamonds(StationActionOffer offer) =>
        offer?.Quote?.PaidCosts?.Any(c => c != null && c.Resource == StationPaymentResource.Diamonds) == true;
    private void Refresh()
    {
        var card = Source.GetCard(_systemId);
        if (!ReferenceEquals(_openedSession, _session.State) || !CanOpenCurrent(card, _operationKind) ||
            card.Station.ConfigId != _stationId || card.Station.Level != _openedLevel)
        { Close(); return; }
        // Placement is semantic, independent of payment-option order in the config.
        var fresh = Source.GetOffers(_systemId).Where(o => o.Kind == _operationKind).ToArray();
        _offers[0] = fresh.FirstOrDefault(o => !UsesDiamonds(o));
        _offers[1] = fresh.FirstOrDefault(UsesDiamonds);
        var current = _selected >= 0 ? _offers[_selected] : null;
        bool expired = _ticket != null && (current == null || !current.Available ||
            current.Revision != _ticket.Offer.Revision || current.Signature() != _ticket.Offer.Signature());
        if (expired) { _ticket = null; Source.CancelConfirmation(); }
        _header.SetText((_researchSource != null ? _researchSource.Title : card.Config != null ? card.Config.DisplayName : "Название станции").ToUpperInvariant());
        var stationSprite = _researchSource != null && _researchSource.Icon != null ? _researchSource.Icon : card.Config != null ? card.Config.StationSprite : null;
        double researchRate = _researchSource?.ScienceRate ?? 0;
        for (int i = 0; i < 2; i++)
        {
            bool selected = i == _selected;
            var group = _groups[i];
            group.Title.SetText(i == 0 ? (_researchSource != null ? "Обычное исследование" : _operationKind == StationOperationKind.Recovery ? "Обычное восстановление" : _operationKind == StationOperationKind.Upgrade ? "Обычное улучшение" : "Обычная постройка") : "Ускоренно");
            group.Button.interactable = true; // Even unaffordable options can be selected and inspected.
            group.Button.GetComponent<Image>().color = selected ? SelectedFill : IdleFill;
            group.Header.color = selected ? StationPanelStyle2A.PrimaryButtonColor : _idleHeader;
            group.Outline.enabled = selected;
            var quote = _offers[i]?.Quote;
            double seconds = quote?.DurationSeconds ?? 0;
            if (_researchSource != null && researchRate > 0) seconds /= researchRate;
            group.Time.SetText(quote == null ? "Время не настроено" : "Время: " +
                (seconds <= 0 ? "мгновенно" : Math.Ceiling(seconds).ToString("0") + " сек."));
        }
        foreach (var resource in _resources)
        {
            var costs = _offers[resource.Group]?.Quote?.PaidCosts;
            long amount = costs?.Where(c => c != null && c.Resource == resource.Resource).Sum(c => (long)c.Amount) ?? 0;
            resource.Amount.SetText(costs == null ? "—" : amount.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")));
            resource.Amount.color = HasResource(costs, resource.Resource) ? Color.white : ShortageColor;
        }
        bool canBuild = current?.Available == true && _ticket != null && !_busy;
        _confirm.interactable = canBuild;
        _confirmText.SetText(_researchSource != null ? "Исследовать" : _operationKind == StationOperationKind.Recovery ? "Восстановить" : _operationKind == StationOperationKind.Upgrade ? "Улучшить до уровня " + (_openedLevel + 1) : "Построить");
        _confirm.GetComponent<Image>().color = canBuild ? StationPanelStyle2A.PrimaryButtonColor : DisabledFill;
        _confirmText.color = canBuild ? Color.white : new Color32(185, 192, 205, 255);
        _notice.SetText(_selected < 0 ? "Выберите способ оплаты" : current == null ? "Оплата не настроена" :
            !current.Available ? StationInterfaceRules.Reason(current.UnavailableReason) :
            _ticket == null ? "Условия изменились. Выберите оплату снова" : string.Empty);
        ApplyLayout();
        StationPanelStyle2A.LayoutStationHeader(_form, _header, _headerIcon, stationSprite, 56f);
    }
    private bool HasResource(StationPaymentCostSnapshot[] costs, StationPaymentResource resource)
    {
        if (costs == null) return false;
        var player = _session.State?.Player;
        if (player == null) return false;
        var required = costs.Where(c => c != null && c.Resource == resource).ToArray();
        if (resource == StationPaymentResource.Credits) return required.Sum(c => (long)c.Amount) <= player.Credits;
        if (resource == StationPaymentResource.Diamonds) return required.Sum(c => (long)c.Amount) <= player.Diamonds;
        return StationPaymentRules.GetSpendableAstralites(player) >= required.Sum(c => (long)c.Amount);
    }
    public void SelectOption(int index)
    {
        if (!_open || _busy || index < 0 || index >= 2) return;
        Source.CancelConfirmation(); _ticket = null; _selected = index;
        var offer = _offers[index];
        if (offer?.Available == true)
            Source.Prepare(_systemId, _operationKind, offer.OptionId, out _ticket, out _);
        Refresh(); Log("PAYMENT_SELECTED_" + index);
    }
    public void ConfirmPayment()
    {
        if (!_open || _busy || _ticket == null) return;
        // Re-read availability immediately; a last-moment wallet or config change must not be paid.
        Refresh();
        if (!_open || _ticket == null || !_confirm.interactable) return;
        _busy = true; string id = _ticket.TicketId; _ticket = null;
        bool research = _researchSource != null;
        try
        {
            if (Source.Confirm(id, out string reason))
            { Close(); _completed?.Invoke(research ? "Исследование запущено" : _operationKind == StationOperationKind.Recovery ? "Станция восстановлена" : _operationKind == StationOperationKind.Upgrade ? "Улучшение запущено" : "Строительство запущено"); Log("PAYMENT_COMMITTED"); }
            else
            { Refresh(); _notice.SetText(StationInterfaceRules.Reason(reason)); Log("PAYMENT_REJECTED"); }
        }
        finally { _busy = false; }
    }
    private bool EnsureUi()
    {
        if (_overlay != null) return true;
        var canvas = _template != null ? _template.GetComponentInParent<Canvas>(true)?.rootCanvas : null;
        if (canvas == null) return false;
        _canvas = canvas.transform as RectTransform;
        _overlay = new GameObject("StationPaymentModal", typeof(RectTransform), typeof(Image), typeof(Button));
        _overlay.transform.SetParent(_canvas, false); Stretch(_overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var dim = _overlay.GetComponent<Image>(); dim.color = new Color(0, 0, 0, .45f);
        var backdrop = _overlay.GetComponent<Button>(); backdrop.targetGraphic = dim; backdrop.transition = Selectable.Transition.None;
        backdrop.onClick.AddListener(Close);
        var form = Instantiate(_template.gameObject, _overlay.transform, false); form.name = "StationPaymentPanel"; form.SetActive(true);
        _form = form.GetComponent<RectTransform>(); _form.anchorMin = _form.anchorMax = new Vector2(.5f, 0); _form.pivot = new Vector2(.5f, 0);
        foreach (var relay in form.GetComponentsInChildren<SystemHudPointerClickRelay2A>(true)) relay.SetReceiver(null);
        var consume = form.AddComponent<Button>(); consume.transition = Selectable.Transition.None;
        _style = form.transform.Find("RowsText")?.GetComponent<TMP_Text>(); if (_style != null) _style.gameObject.SetActive(false);
        _header = form.transform.Find("HeaderText")?.GetComponent<TMP_Text>();
        _header.color = Color.white; _header.raycastTarget = false;
        _header.rectTransform.anchorMin = Vector2.up; _header.rectTransform.anchorMax = Vector2.one;
        _header.rectTransform.pivot = new Vector2(.5f, 1); _header.rectTransform.sizeDelta = new Vector2(0, 48);
        _header.rectTransform.anchoredPosition = Vector2.zero;
        _headerIcon = StationPanelStyle2A.HeaderIcon(_form);
        var panelImage = form.GetComponent<Image>();
        var sourceHeader = form.transform.Find("HeaderBackground")?.GetComponent<Image>();
        _idleHeader = sourceHeader != null ? sourceHeader.color : new Color32(64, 5, 9, 255);
        var close = StationPanelStyle2A.Button(_form, _style, _idleHeader, "", Close, out var closeText);
        Fixed(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.zero, new Vector2(48, 48));
        var closeIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); closeIcon.transform.SetParent(close.transform, false);
        Stretch(closeIcon.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var closeImage = closeIcon.GetComponent<Image>(); closeImage.sprite = Resources.Load<Sprite>("ui_panel_close_01");
        closeImage.preserveAspect = true; closeImage.raycastTarget = false;
        if (closeImage.sprite == null) closeText.SetText("×");
        for (int i = 0; i < 2; i++)
        {
            int index = i; var group = new PaymentGroup(); _groups[i] = group;
            group.Button = StationPanelStyle2A.Button(_form, _style, IdleFill, "", () => SelectOption(index), out var unused);
            group.Button.name = "PaymentCard_" + i; unused.gameObject.SetActive(false);
            group.Button.transition = Selectable.Transition.None;
            group.Button.GetComponent<Image>().sprite = panelImage != null ? panelImage.sprite : null;
            group.Button.GetComponent<Image>().type = Image.Type.Sliced;
            group.Outline = group.Button.gameObject.AddComponent<Outline>(); group.Outline.effectColor = StationPanelStyle2A.PrimaryButtonColor;
            group.Outline.effectDistance = new Vector2(2, 2); group.Outline.enabled = false;
            var heading = new GameObject("CardHeader", typeof(RectTransform), typeof(Image)); heading.transform.SetParent(group.Button.transform, false);
            group.Header = heading.GetComponent<Image>(); group.Header.raycastTarget = false;
            var rect = heading.GetComponent<RectTransform>(); rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(0, -44); rect.offsetMax = Vector2.zero;
            group.Title = NewText(heading.transform, "Title", i == 0 ? "Обычная постройка" : "Ускоренно");
            Stretch(group.Title.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));
            group.Time = NewText(group.Button.transform, "Time", "");
            rect = group.Time.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right;
            rect.pivot = new Vector2(.5f, 0); rect.offsetMin = new Vector2(4, 0); rect.offsetMax = new Vector2(-4, 44);
            var rule = new GameObject("TimeDivider", typeof(RectTransform), typeof(Image)); rule.transform.SetParent(group.Button.transform, false);
            rule.GetComponent<Image>().color = new Color(1, 1, 1, .12f); rule.GetComponent<Image>().raycastTarget = false;
            rect = rule.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right;
            rect.offsetMin = new Vector2(0, 43); rect.offsetMax = new Vector2(0, 44);
        }
        var divider = new GameObject("ResourceDivider", typeof(RectTransform), typeof(Image)); divider.transform.SetParent(_groups[0].Button.transform, false);
        divider.GetComponent<Image>().color = new Color(1, 1, 1, .12f); divider.GetComponent<Image>().raycastTarget = false;
        var dividerRect = divider.GetComponent<RectTransform>(); dividerRect.anchorMin = new Vector2(.5f, 0); dividerRect.anchorMax = new Vector2(.5f, 1);
        dividerRect.offsetMin = new Vector2(-.5f, 44); dividerRect.offsetMax = new Vector2(.5f, -44);
        _resources[0] = CreateResource(0, StationPaymentResource.Credits, "Кредиты");
        _resources[1] = CreateResource(0, StationPaymentResource.Astralites, "Астралит");
        _resources[2] = CreateResource(1, StationPaymentResource.Diamonds, "Алмазы");
        _notice = NewText(_form, "PaymentNotice", "");
        _confirm = StationPanelStyle2A.Button(_form, _style, DisabledFill, "Построить", ConfirmPayment, out _confirmText);
        _confirm.name = "BuildStation"; _confirm.transition = Selectable.Transition.None;
        _confirmText.alignment = TextAlignmentOptions.Center;
        _overlay.SetActive(false); return true;
    }
    private ResourceCard CreateResource(int group, StationPaymentResource resource, string title)
    {
        var root = new GameObject("Resource_" + resource, typeof(RectTransform)); root.transform.SetParent(_groups[group].Button.transform, false);
        var card = new ResourceCard { Rect = root.GetComponent<RectTransform>(), Resource = resource, Group = group };
        card.Label = NewText(root.transform, "Label", title); card.Amount = NewText(root.transform, "Amount", "");
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(root.transform, false);
        card.Icon = icon.GetComponent<Image>(); card.Icon.preserveAspect = true; card.Icon.raycastTarget = false;
        card.Icon.sprite = _paymentIcon?.Invoke(resource) ?? (resource == StationPaymentResource.Astralites ? Resources.Load<Sprite>("icon_astralite_generic_01") : null);
        return card;
    }
    private TMP_Text NewText(Transform parent, string name, string value)
    {
        var text = StationPanelStyle2A.Text(parent, _style, name, value); text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true;
        text.fontSizeMin = 14; text.fontSizeMax = _style != null ? _style.fontSize : 24;
        return text;
    }
    private void ApplyLayout()
    {
        float bottom = Mathf.Min(158f, Mathf.Max(8f, _canvas.rect.height * .1f));
        float available = Mathf.Max(1f, _canvas.rect.height - bottom - 16f);
        float unit = Mathf.Max(1f, Mathf.Min(288f, (_canvas.rect.width - 74f) / 3f, available - 248f));
        float width = unit * 3 + 42; float groupHeight = unit + 84;
        _form.sizeDelta = new Vector2(width, Mathf.Min(available, groupHeight + 164)); _form.anchoredPosition = new Vector2(0, bottom);
        Fixed(_groups[0].Button.GetComponent<RectTransform>(), Vector2.up, Vector2.up, new Vector2(16, -56), new Vector2(unit * 2, groupHeight));
        Fixed(_groups[1].Button.GetComponent<RectTransform>(), Vector2.up, Vector2.up, new Vector2(16 + unit * 2 + 10, -56), new Vector2(unit, groupHeight));
        for (int i = 0; i < 3; i++)
        {
            var resource = _resources[i]; float x = i == 1 ? unit : 0;
            Fixed(resource.Rect, Vector2.up, Vector2.up, new Vector2(x, -44), new Vector2(unit, groupHeight - 88));
            Fixed(resource.Label.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(unit - 12, 32));
            Fixed(resource.Icon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(unit * .58f, unit * .58f));
            Fixed(resource.Amount.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(unit - 12, 40));
        }
        Fixed(_notice.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 68), new Vector2(width - 32, 40));
        Fixed(_confirm.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(16, 12), new Vector2(width - 32, 48));
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max; }
    private static void Fixed(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
    private static void Log(string action) => AppLog.Info(DebugLogChannel.StationInterface, "[StationInterface] " + action);
}
