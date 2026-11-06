using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Single-click station card on the system Canvas, themed from the live planet-list panel.</summary>
public sealed class SystemStationPanel2A : MonoBehaviour
{
    private GameObject _panel;
    private Button _constructionButton;
    private Image _headerIcon;
    private StationConstructionPaymentPanel2A _constructionPayment;
    private RectTransform _panelRect, _content;
    private Transform _navigation, _actions;
    private TMP_Text _header, _body, _notice, _style, _researchProgress;
    private IStationInfrastructureService _infrastructure;
    private Color _panelColor;
    private SimpleEventBus _events;
    private IStationCardService _cards;
    private IStationVisitService _visits;
    private IStationFacilityService _facilities;
    private IResearchService _research;
    private IGameSessionService _session;
    private IConfigService _config;
    private IInventoryService _inventory;
    private IEconomyService _economy;
    private IRefuelService _refuel;
    private IRepairService _repair;
    private string _systemId, _stationId, _tab = "card", _layoutKey, _message = string.Empty;
    private StationActionConfirmation _confirmation;
    private ResearchUpgrade _researchUpgrade;
    private StationOperationRuntimeState _researchQuote;
    private long _researchRevision;
    private GameRuntimeState _researchSession;
    private bool _initialized, _busy;
    private float _nextRefresh;
    private readonly List<ActionRow> _rows = new List<ActionRow>();
    private sealed class ActionRow
    {
        public Button Button;
        public TMP_Text Text;
        public StationOperationKind Kind;
        public string OptionId;
        public ResearchNode Node;
    }
    public void Initialize()
    {
        if (_initialized || Bootstrapper.Instance?.ServiceRegistry == null) return;
        var r = Bootstrapper.Instance.ServiceRegistry;
        _events = r.Get<SimpleEventBus>(); _cards = r.Get<IStationCardService>(); _visits = r.Get<IStationVisitService>();
        _infrastructure = r.Get<IStationInfrastructureService>();
        _facilities = r.Get<IStationFacilityService>(); _research = r.Get<IResearchService>(); _session = r.Get<IGameSessionService>();
        _config = r.Get<IConfigService>(); _inventory = r.Get<IInventoryService>(); _economy = r.Get<IEconomyService>();
        _refuel = r.Get<IRefuelService>(); _repair = r.Get<IRepairService>();
        var objects = FindFirstObjectByType<SystemObjectsListHud2A>(FindObjectsInactive.Include);
        var source = objects != null ? objects.transform.Find("SystemObjectsListPanel") : null;
        if (source == null)
        {
            AppLog.Warning(DebugLogChannel.StationInterface, "[StationInterface] Planet-list style panel unavailable"); return;
        }
        _panel = StationPanelStyle2A.Clone(source); _panelRect = _panel.GetComponent<RectTransform>();
        _header = _panel.transform.Find("HeaderText")?.GetComponent<TMP_Text>();
        _style = _panel.transform.Find("RowsText")?.GetComponent<TMP_Text>();
        if (_header == null || _style == null) { Destroy(_panel); _panel = null; return; }
        _style.gameObject.SetActive(false);
        _headerIcon = StationPanelStyle2A.HeaderIcon(_panelRect);
        var image = _panel.GetComponent<Image>(); _panelColor = image != null ? image.color : Color.clear;
        var close = _header.gameObject.GetComponent<Button>() ?? _header.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None; close.onClick = new Button.ButtonClickedEvent(); close.onClick.AddListener(Close);
        _content = StationPanelStyle2A.ScrollContent(_panelRect, _panelColor);
        _body = StationPanelStyle2A.Text(_content, _style, "StationDetails", string.Empty);
        _notice = StationPanelStyle2A.Text(_content, _style, "StationNotice", string.Empty);
        _navigation = Group(_content, "StationNavigation"); _actions = Group(_content, "StationActions");
        _constructionButton = StationPanelStyle2A.ConstructionFooter(_panelRect, _style, _panelColor, () =>
        {
            if (_busy) return;
            var current = _cards.GetCard(_systemId);
            if (IsDestroyedOverview(current))
            {
                if (!_constructionPayment.OpenRecovery(_systemId))
                { _message = "Панель оплаты недоступна"; Refresh(); }
            }
            else if (IsBuiltOverview(current))
            {
                if (CanShowUpgrade(current)) _constructionPayment.OpenUpgrade(_systemId);
            }
            else if (!_constructionPayment.Open(_systemId))
            { _message = "Панель оплаты недоступна"; Refresh(); }
        });
        _constructionPayment = GetComponent<StationConstructionPaymentPanel2A>() ?? gameObject.AddComponent<StationConstructionPaymentPanel2A>();
        _constructionPayment.Initialize(source, _cards, _session, r.Get<IGameTimeService>(),
            id => _config.GetItemConfigById(id)?.DisplayName,
            message => { _message = message; _layoutKey = null; if (_panel != null && _panel.activeSelf) Refresh(); },
            resource => resource == StationPaymentResource.Credits ? _config.GetItemConfigById("item_credits_01")?.Icon :
                resource == StationPaymentResource.Diamonds ? _config.GetItemConfigById("item_diamonds_01")?.Icon : Resources.Load<Sprite>("icon_astralite_generic_01"));
        _events.Subscribe<StationSelectedEvent>(OnSelected);
        _events.Subscribe<StationCardRequestedEvent>(OnRequested);
        _events.Subscribe<StationVisitChangedEvent>(OnVisit);
        _events.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(OnClose);
        _events.Subscribe<SystemObjectsPanelRequestedEvent2A>(OnObjects);
        _events.Subscribe<PlanetEnteredEvent>(OnPlanet);
        _events.Subscribe<GalaxyEnteredEvent>(OnGalaxy);
        _events.Subscribe<StarSystemEnteredEvent>(OnSystem);
        _initialized = true;
    }
    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup)); go.transform.SetParent(parent, false);
        var layout = go.GetComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childControlHeight = true;
        layout.childControlWidth = true; layout.childForceExpandHeight = false; return go.transform;
    }
    private void OnDestroy()
    {
        if (_initialized)
        {
            _events.Unsubscribe<StationSelectedEvent>(OnSelected); _events.Unsubscribe<StationCardRequestedEvent>(OnRequested);
            _events.Unsubscribe<StationVisitChangedEvent>(OnVisit); _events.Unsubscribe<SystemObjectsPanelCloseRequestedEvent2A>(OnClose);
            _events.Unsubscribe<SystemObjectsPanelRequestedEvent2A>(OnObjects); _events.Unsubscribe<PlanetEnteredEvent>(OnPlanet);
            _events.Unsubscribe<GalaxyEnteredEvent>(OnGalaxy); _events.Unsubscribe<StarSystemEnteredEvent>(OnSystem);
        }
        _constructionPayment?.Close(); _cards?.CancelConfirmation(); if (_panel != null) Destroy(_panel);
    }
    private void OnSelected(StationSelectedEvent evt) { if (evt?.Station != null) Open(evt.Station); }
    private void OnRequested(StationCardRequestedEvent evt)
    {
        if (evt?.Station == null) return;
        if (_panel != null && _panel.activeSelf && _stationId == evt.Station.Id &&
            _systemId == _session?.State?.Player?.CurrentSystemId)
        {
            // Arrival updates the existing card. Open would broadcast a close request and reset its state.
            _layoutKey = null; _nextRefresh = 0f;
            AppLog.Info(DebugLogChannel.StationInterface, "[StationInterface] CARD_ARRIVAL_REFRESH | Station=" + _stationId);
            return;
        }
        Open(evt.Station);
    }
    private void OnClose(SystemObjectsPanelCloseRequestedEvent2A evt) => Close();
    private void OnObjects(SystemObjectsPanelRequestedEvent2A evt) => Close();
    private void OnPlanet(PlanetEnteredEvent evt) => Close();
    private void OnGalaxy(GalaxyEnteredEvent evt) => Close();
    private void OnSystem(StarSystemEnteredEvent evt) => Close();
    private void OnVisit(StationVisitChangedEvent evt)
    {
        if (evt.Inside || evt.ForcedExit) { Close(); return; }
        if (_panel != null && _panel.activeSelf && evt.SystemId == _systemId) { _layoutKey = null; Refresh(); }
    }
    public void ShowDockedIfAny()
    {
        if (!_initialized) Initialize();
        var player = _session?.State?.Player;
        if (player != null && !string.IsNullOrEmpty(player.CurrentStationId))
        {
            var station = _config.GetStarSystemConfigById(player.CurrentSystemId)?.Station;
            if (station != null && station.Id == player.CurrentStationId) Open(station);
        }
    }
    public void Open(StationConfig station)
    {
        if (!_initialized) Initialize();
        if (_panel == null || station == null || _session.State?.Player == null) return;
        string system = _session.State.Player.CurrentSystemId;
        if (_config.GetStarSystemConfigById(system)?.Station != station) return;
        _events.Publish(new SystemObjectsPanelCloseRequestedEvent2A());
        // The objects-panel close event already hides the old information panel.
        // Explicit target-panel close would also clear the station selection/marker.
        _systemId = system; _stationId = station.Id; _tab = "card"; _message = string.Empty;
        _layoutKey = null; ClearConfirmation(); _panel.SetActive(true); _panel.transform.SetAsLastSibling(); Refresh();
        AppLog.Info(DebugLogChannel.StationInterface, "[StationInterface] CARD_OPENED | Station=" + station.Id);
    }
    private void OnDisable() => _constructionPayment?.Close();
    public void Close()
    {
        _constructionPayment?.Close();
        if (_panel != null) _panel.SetActive(false);
        ClearConfirmation(); _layoutKey = null;
    }
    private void ClearConfirmation()
    {
        _confirmation = null; _researchQuote = null; _researchSession = null; _researchUpgrade = ResearchUpgrade.None; _cards?.CancelConfirmation();
    }
    private void Update()
    {
        if (!_initialized || _panel == null || !_panel.activeSelf || _busy || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .15f; Refresh();
    }
    private void Refresh()
    {
        var card = _cards.GetCard(_systemId);
        if (card == null || card.Station.ConfigId != _stationId) { Close(); return; }
        if (_confirmation != null && _confirmation.Offer.Revision != card.Station.Revision)
        { ClearConfirmation(); _message = StationInterfaceRules.Reason("StationStateChanged"); _layoutKey = null; }
        var science = _research.GetSnapshot();
        if (_researchQuote != null && science?.Revision != _researchRevision)
        { ClearConfirmation(); _message = "Состояние исследования изменилось. Выберите действие снова"; _layoutKey = null; }
        if (!card.IsInside && _tab != "card") { _tab = "card"; ClearConfirmation(); _layoutKey = null; }
        _header.color = Color.white;
        _header.SetText(card.Config.DisplayName.ToUpperInvariant());
        var headerSprite = card.Station.IsDestroyed && card.Config.DestroyedSprite != null ? card.Config.DestroyedSprite : card.Config.StationSprite;
        StationPanelStyle2A.LayoutStationHeader(_panelRect, _header, _headerIcon, headerSprite);
        bool developing = StationPanelStyle2A.IsDevelopmentRunning(card.Station);
        bool builtOverview = IsBuiltOverview(card);
        bool destroyedOverview = IsDestroyedOverview(card);
        if (builtOverview || destroyedOverview) _tab = "card";
        if (developing || builtOverview || destroyedOverview) _message = string.Empty;
        _body.SetText(Describe(card)); _notice.SetText(_message);
        Fit(_body); Fit(_notice); _notice.gameObject.SetActive(!developing && !builtOverview && !destroyedOverview && !string.IsNullOrEmpty(_message));
        var offers = _cards.GetOffers(_systemId);
        string key = _tab + ":" + builtOverview + ":" + IsConstructionOverview(card) + ":" + card.IsInside + ":" + card.Station.Level + ":" + card.Station.IsDestroyed + ":" +
            (_confirmation?.TicketId ?? "") + ":" + (_researchQuote?.OperationId ?? "") + ":" +
            string.Join(";", offers.Select(x => x.Kind + ":" + x.OptionId));
        if (_tab == "research" && science != null) key += ":" + (science.Active?.OperationId ?? "idle") + ":" +
            string.Join(";", science.Levels.Where(x => x != null).Select(x => x.Upgrade + ":" + x.Level));
        if (_layoutKey != key) { _layoutKey = key; Rebuild(card, offers, science); }
        foreach (var row in _rows)
        {
            if (row.Node == null)
            {
                var offer = offers.FirstOrDefault(x => x.Kind == row.Kind && x.OptionId == row.OptionId);
                row.Button.interactable = offer?.Available == true; row.Text.SetText(OfferText(offer));
                FitButton(row.Button, row.Text);
            }
            else
            {
                bool valid = science != null && ResearchRules.TryQuote(_research.Tree, science, row.Node.Upgrade, row.OptionId, out _, out _);
                var next = row.Node.Levels?.FirstOrDefault(x => x != null && x.Level == (science?.GetLevel(row.Node.Upgrade) ?? 0) + 1);
                var option = next?.PaymentOptions?.FirstOrDefault(x => x != null && x.Id == row.OptionId);
                if (option != null && option.TryCreateSnapshot(StationOperationKind.Upgrade, next.Level, out var quote))
                {
                    bool afford = StationPaymentRules.CanAfford(_session.State.Player, quote, out _);
                    row.Button.interactable = valid && afford;
                    row.Text.SetText("Исследовать уровень " + next.Level + "\n" + Costs(quote) + " · базовое время " + Duration(quote.DurationSeconds) +
                        (!afford ? "\nНедостаточно ресурсов" : !valid ? "\nИсследование сейчас недоступно" : ""));
                    FitButton(row.Button, row.Text);
                }
            }
        }
        if (_researchProgress != null && science?.Active != null)
        {
            var active = science.Active; double rate = _infrastructure.GetScienceRate();
            string title = ResearchRules.Find(_research.Tree, science.ActiveUpgrade)?.Title ?? "Исследование";
            _researchProgress.SetText(title + " → " + active.TargetLevel + "\n" +
                (rate <= 0 ? "Приостановлено: нет научного вклада" : "Осталось примерно " + ((active.DurationSeconds - active.ElapsedSeconds) / rate).ToString("0") + " сек."));
            Fit(_researchProgress);
        }

        bool overview = IsConstructionOverview(card) && _confirmation == null;
        bool showFooter = !card.IsCaptured && (overview || developing || destroyedOverview || CanShowUpgrade(card));
        _constructionButton.gameObject.SetActive(showFooter);
        StationPanelStyle2A.UpdateConstructionFooter(_constructionButton, card.Station, card.HasEnemies);
        if (builtOverview)
        {
            _constructionButton.GetComponentInChildren<TMP_Text>(true).SetText("Улучшить до уровня " + (card.Station.Level + 1));
            _constructionButton.interactable = StationLifecycleRules.CanBegin(card.Station, StationOperationKind.Upgrade, out _);
            _constructionButton.GetComponent<Image>().color = _constructionButton.interactable
                ? StationPanelStyle2A.PrimaryButtonColor : new Color32(65, 73, 90, 255);
        }
        if (destroyedOverview)
        {
            _constructionButton.GetComponentInChildren<TMP_Text>(true).SetText("Восстановить");
            _constructionButton.interactable = card.Station.Level > 0 && card.Station.Recovery == null;
            _constructionButton.GetComponent<Image>().color = _constructionButton.interactable
                ? StationPanelStyle2A.PrimaryButtonColor : new Color32(65, 73, 90, 255);
        }
        // Reserve space below the scroll area so neither text nor its mask covers the action.
        var scrollRect = _content.parent.parent.GetComponent<RectTransform>();
        scrollRect.offsetMin = new Vector2(10, showFooter ? 88 : 10);
        // Fit the description and current actions; reserve the fixed footer only while it is shown.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        float contentHeight = LayoutUtility.GetPreferredHeight(_content);
        float desiredHeight = Mathf.Clamp(contentHeight + 56f + scrollRect.offsetMin.y, 180f, 800f);
        StationPanelStyle2A.FitHeightToCanvas(_panelRect, desiredHeight);
    }
    private static bool IsDestroyedOverview(StationCardSnapshot card) => card?.Station?.IsDestroyed == true;
    private static bool IsBuiltOverview(StationCardSnapshot card) => card?.Station != null &&
        card.Station.Level >= 1 && card.Station.Level <= 10 && !card.Station.IsDestroyed &&
        card.Station.Development == null && card.Station.Recovery == null;
    private static bool CanShowUpgrade(StationCardSnapshot card)
    {
        if (card.IsCaptured || !IsBuiltOverview(card) || card.Station.Level >= 10 || card.Config == null) return false;
        int required = card.Config.LevelEffects?.GetLevel(card.Station.Level + 1)?.RequiredGalaxyLevel ?? 1;
        return card.GalaxyLevel >= required;
    }
    private static bool IsConstructionOverview(StationCardSnapshot card) =>
        StationPanelStyle2A.IsAwaitingConstruction(card.Station);
    private string Describe(StationCardSnapshot card)
    {
        var s = card.Station;
        string metric = StationInterfaceRules.StationMetricText(card);
        string metricLine = string.IsNullOrEmpty(metric) ? string.Empty : "\n" + metric;
        if (IsDestroyedOverview(card))
            return "Тип станции: " + StationInterfaceRules.TypeName(s.StationType) +
                "\nСостояние: Уровень станции " + s.Level + "/" + StationLifecycleRules.MaxLevel +
                "\nЗдоровье: " + s.Health + " / " + s.MaxHealth +
                metricLine +
                "\nПовреждение: разрушена";
        if (IsBuiltOverview(card))
            return "Тип станции: " + StationInterfaceRules.TypeName(s.StationType) +
                "\nСостояние: Уровень станции: " + s.Level + "/10" +
                "\nЗдоровье: " + s.Health + " / " + s.MaxHealth +
                metricLine +
                "\nПовреждения: " + (card.Damage > 0 ? Mathf.Clamp(card.Damage, 0, 100) + "%" : "нет");
        if (StationPanelStyle2A.IsUpgradeRunning(s))
            return "Тип станции: " + StationInterfaceRules.TypeName(s.StationType) +
                "\nСостояние: улучшение до уровня " + s.Development.TargetLevel +
                "\nЗдоровье: " + s.Health + " / " + s.MaxHealth +
                metricLine +
                "\nПовреждения: " + (card.Damage > 0 ? Mathf.Clamp(card.Damage, 0, 100) + "%" : "нет");
        if (StationPanelStyle2A.IsConstructionRunning(s))
            return "Тип станции: " + StationInterfaceRules.TypeName(s.StationType) + "\nСостояние: строительство" +
                metricLine;
        if (IsConstructionOverview(card))
            return "Тип станции: " + StationInterfaceRules.TypeName(s.StationType) + "\n<color=" +
                StationPanelStyle2A.AwaitingConstructionTextColor + ">Состояние: ожидает постройки</color>" +
                metricLine;
        var text = new StringBuilder();
        text.Append(StationInterfaceRules.TypeName(s.StationType)).Append(" · Уровень ").Append(s.Level).Append("/10");
        text.Append("\nСостояние: ").Append(s.IsDestroyed ? "разрушена" : s.Level == 0 ? "не построена" : !s.IsEnabled ? "отключена" : card.Damage > 0 ? "повреждена" : "работает");
        text.Append(" · Повреждение: ").Append(card.Damage).Append('%');
        if (!card.IsInside) text.Append("\nДо станции: ").Append(card.Distance.ToString("0")).Append(" ед.");
        if (s.ConfigMissing) text.Append("\nНастройки станции недоступны");
        if (s.Development != null) text.Append("\n").Append(StationInterfaceRules.ActionName(s.Development.Kind)).Append(": ")
            .Append(s.Development.ElapsedSeconds.ToString("0")).Append(" / ").Append(s.Development.DurationSeconds.ToString("0")).Append(" сек.")
            .Append(s.Development.PauseReason != StationOperationPauseReason.None ? " · приостановлено" : "");
        if (s.Recovery != null) text.Append("\nВосстановление: ").Append(s.Recovery.ElapsedSeconds.ToString("0")).Append(" / ")
            .Append(s.Recovery.DurationSeconds.ToString("0")).Append(" сек.").Append(s.Recovery.PauseReason != StationOperationPauseReason.None ? " · приостановлено" : "");
        var settings = card.Config.LevelEffects?.GetLevel(s.Level);
        if (card.Damage > 0 && !s.IsDestroyed && settings != null)
            text.Append("\nСаморемонт: ").Append(card.HasEnemies ? "остановлен — в системе враги" :
                settings.HealthPerTick <= 0 ? "отключён" : settings.HealthPerTick + " здоровья за тик");
        if (s.Level == 10) text.Append("\nДостигнут максимальный уровень");
        if (s.IsOperational && !s.ConfigMissing && !card.IsCaptured && settings != null)
        {
            float efficiency = settings.Efficiency(card.Damage);
            switch (s.StationType)
            {
                case StationType.Military:
                    int count = (settings.Population?.AllySpawnRules ?? Array.Empty<AllySpawnRuleConfig>()).Where(x => x != null)
                        .Sum(rule => rule.GetAlliesForGalaxyLevel(card.GalaxyLevel).Where(x => x?.AllyConfig?.Role == AllyRole2A.Military).Sum(x => x.MaxCount));
                    text.Append("\nВоенные базы: до ").Append(count).Append(" · Пополнение: ").Append(efficiency.ToString("0%")); break;
                case StationType.RangerBase:
                    text.Append("\nВероятность «Атаковать врага» у рейнджеров сектора: ").Append((settings.RangerAttackProbability * efficiency).ToString("0%")); break;
                case StationType.Science:
                    text.Append("\nВклад в скорость исследований: ").Append((settings.ScienceRate * efficiency).ToString("0.##")); break;
                case StationType.Trade: text.Append("\nОсобое влияние появится вместе с экономикой"); break;
                case StationType.Medical: text.Append("\nОсобое влияние появится вместе с болезнями"); break;
            }
        }
        if (!s.IsOperational || s.ConfigMissing || card.IsCaptured) text.Append("\nЭффекты станции отключены");
        return text.ToString();
    }
    private void Fit(TMP_Text text)
    {
        var element = text.GetComponent<LayoutElement>();
        element.preferredHeight = Mathf.Max(34, text.GetPreferredValues(text.text, Mathf.Max(120, _panelRect.rect.width - 40), Mathf.Infinity).y + 12);
    }
    private void FitButton(Button button, TMP_Text text)
    {
        float width = Mathf.Max(120, _panelRect.rect.width - 68);
        button.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(58, text.GetPreferredValues(text.text, width, Mathf.Infinity).y + 16);
    }
    private static void Clear(Transform group)
    {
        foreach (Transform child in group) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    }
    private void Nav(string title, Action callback) => StationPanelStyle2A.Button(_navigation, _style, _panelColor, title, () => callback(), out _);
    private void ActionButton(string title, Action callback, out TMP_Text text, out Button button)
    { button = StationPanelStyle2A.Button(_actions, _style, _panelColor, title, () => callback(), out text); FitButton(button, text); }
    private void Rebuild(StationCardSnapshot card, IReadOnlyList<StationActionOffer> offers, ResearchRuntimeState science)
    {
        Clear(_navigation); Clear(_actions); _rows.Clear(); _researchProgress = null;
        var scroll = _content.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.StopMovement();
        _content.anchoredPosition = Vector2.zero;
        if (StationPanelStyle2A.IsDevelopmentRunning(card.Station) || IsBuiltOverview(card) || IsDestroyedOverview(card)) return;
        if (card.IsInside)
        {
            Nav("Карточка станции", () => Tab("card")); Nav("Заправка, ремонт корабля и рынок", () => Tab("services"));
            if (card.Station.StationType == StationType.Science) Nav("Исследования галактики", () => { var location = FindFirstObjectByType<PlanetController>(FindObjectsInactive.Include); if (location != null && location.OpenStationResearch()) Close(); });
            Nav("Покинуть станцию", () => { _visits.Leave(); Tab("card"); });
        }
        if (_confirmation != null)
        {
            var text = StationPanelStyle2A.Text(_actions, _style, "StationConfirmation", "Подтвердить действие?\n" + OfferText(_confirmation.Offer)); Fit(text);
            ActionButton("Подтвердить оплату", ConfirmStation, out _, out _);
            ActionButton("Отмена", () => { ClearConfirmation(); _layoutKey = null; Refresh(); }, out _, out _); return;
        }
        if (_researchQuote != null)
        {
            var text = StationPanelStyle2A.Text(_actions, _style, "ResearchConfirmation", "Подтвердить исследование?\n" +
                (ResearchRules.Find(_research.Tree, _researchUpgrade)?.Title ?? "Исследование") + " · Уровень " + _researchQuote.TargetLevel + "\n" + Costs(_researchQuote) + " · " + Duration(_researchQuote.DurationSeconds)); Fit(text);
            ActionButton("Подтвердить оплату", ConfirmResearch, out _, out _);
            ActionButton("Отмена", () => { ClearConfirmation(); _layoutKey = null; Refresh(); }, out _, out _); return;
        }
        if (_tab == "services") { BuildServices(card); return; }
        if (_tab == "research") { BuildResearch(science); return; }
        if (IsConstructionOverview(card)) return; // Construction payment is a separate modal.
        foreach (var offer in offers)
        {
            var row = new ActionRow { Kind = offer.Kind, OptionId = offer.OptionId };
            ActionButton(OfferText(offer), () => PrepareStation(row.Kind, row.OptionId), out row.Text, out row.Button); _rows.Add(row);
        }
    }
    private void Tab(string tab) { ClearConfirmation(); _tab = tab; _layoutKey = null; _message = string.Empty; Refresh(); }
    private void PrepareStation(StationOperationKind kind, string option)
    {
        if (_busy) return;
        if (!_cards.Prepare(_systemId, kind, option, out _confirmation, out string reason)) _message = StationInterfaceRules.Reason(reason);
        else _message = string.Empty;
        _layoutKey = null; Refresh();
    }
    private void ConfirmStation()
    {
        if (_busy || _confirmation == null) return;
        string ticket = _confirmation.TicketId; _confirmation = null; _busy = true;
        try { _message = _cards.Confirm(ticket, out string reason) ? "Действие выполнено или запущено" : StationInterfaceRules.Reason(reason); }
        finally { _busy = false; _layoutKey = null; Refresh(); }
    }
    private void BuildResearch(ResearchRuntimeState state)
    {
        if (state == null) return;
        if (state.Active != null)
        {
            var title = ResearchRules.Find(_research.Tree, state.ActiveUpgrade)?.Title ?? "Исследование";
            var text = StationPanelStyle2A.Text(_actions, _style, "ResearchProgress", title + " → " + state.Active.TargetLevel + "\n" +
                state.Active.ElapsedSeconds.ToString("0") + " / " + state.Active.DurationSeconds.ToString("0") + " работы"); _researchProgress = text; Fit(text);
        }
        foreach (var branch in _research.Tree?.Branches ?? Array.Empty<ResearchBranchConfig>())
        {
            if (branch == null) continue;
            var title = StationPanelStyle2A.Text(_actions, _style, "ResearchBranch", branch.DisplayName); Fit(title);
            foreach (var node in branch.Nodes ?? Array.Empty<ResearchNode>())
            {
                if (node == null) continue;
                int level = state.GetLevel(node.Upgrade);
                string deps = string.Join(", ", ResearchRules.DependenciesFor(node, level + 1).Where(d => d != null).Select(d =>
                    (ResearchRules.Find(_research.Tree, d.Upgrade)?.Title ?? "Улучшение") + " " + d.RequiredLevel));
                var text = StationPanelStyle2A.Text(_actions, _style, "ResearchNode", node.Title + " · " + level + "/10" +
                    (string.IsNullOrEmpty(deps) ? "" : "\nТребуется: " + deps)); Fit(text);
                var next = node.Levels?.FirstOrDefault(x => x != null && x.Level == level + 1);
                foreach (var option in next?.PaymentOptions ?? Array.Empty<StationPaymentOption>())
                {
                    if (option == null) continue;
                    var row = new ActionRow { Node = node, OptionId = option.Id };
                    ActionButton("Исследовать", () => PrepareResearch(node.Upgrade, row.OptionId), out row.Text, out row.Button); _rows.Add(row);
                }
            }
        }
    }
    private void PrepareResearch(ResearchUpgrade upgrade, string option)
    {
        var state = _research.GetSnapshot();
        if (!ResearchRules.TryQuote(_research.Tree, state, upgrade, option, out var quote, out string reason) ||
            !StationPaymentRules.CanAfford(_session.State.Player, quote, out reason))
        { _message = StationInterfaceRules.Reason(reason); return; }
        ClearConfirmation(); _researchUpgrade = upgrade; _researchQuote = quote.Copy(); _researchRevision = state.Revision; _researchSession = _session.State; _layoutKey = null; Refresh();
    }
    private void ConfirmResearch()
    {
        if (_busy || _researchQuote == null) return;
        _busy = true; var quote = _researchQuote; var upgrade = _researchUpgrade; long revision = _researchRevision; var session = _researchSession; ClearConfirmation();
        try
        {
            var state = _research.GetSnapshot();
            if (!ReferenceEquals(session, _session.State) || state.Revision != revision || !ResearchRules.TryQuote(_research.Tree, state, upgrade, quote.PaymentOptionId, out var fresh, out _) ||
                PaymentSignature(fresh) != PaymentSignature(quote)) _message = StationInterfaceRules.Reason("QuoteChanged");
            else _message = _research.TryStart(_systemId, upgrade, quote.PaymentOptionId, revision, out string reason)
                ? "Исследование запущено" : StationInterfaceRules.Reason(reason);
        }
        finally { _busy = false; _layoutKey = null; Refresh(); }
    }
    private static string PaymentSignature(StationOperationRuntimeState quote) => quote.TargetLevel + ":" + quote.DurationSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" +
        string.Join(";", quote.PaidCosts.Select(c => c.Resource + ":" + c.ItemConfigId + ":" + c.Amount));
    private void BuildServices(StationCardSnapshot card)
    {
        ActionButton("Заправить до полного · " + _refuel.GetFuelUnitPrice() + " кр./ед.", () =>
            Facility(() => { bool ok = _facilities.TryRefuel(out string reason); return ok ? "Заправка выполнена" : StationInterfaceRules.Reason(reason); }), out _, out _);
        ActionButton("Ремонт корабля · " + _repair.GetRepairUnitPrice() + " кр./ед. корпуса", () =>
            Facility(() => { bool ok = _facilities.TryRepairHull(out string reason); return ok ? "Корабль отремонтирован" : StationInterfaceRules.Reason(reason); }), out _, out _);
        var profile = StationLocationRules.Market(_session.State, _config.GetStarSystemConfigById(card.SystemId));
        if (profile?.Items == null) { var text = StationPanelStyle2A.Text(_actions, _style, "MarketUnavailable", "Ассортимент рынка не настроен"); Fit(text); return; }
        foreach (var entry in profile.Items)
        {
            if (entry?.ItemConfig == null || !entry.IsAvailable) continue;
            string id = entry.ItemConfig.Id;
            var text = StationPanelStyle2A.Text(_actions, _style, "MarketItem", entry.ItemConfig.DisplayName); Fit(text);
            ActionButton("Купить 1 · " + _economy.GetBuyPrice(id) + " кр.", () =>
                Facility(() => { bool ok = _facilities.TryBuy(id, 1, out string reason); return ok ? "Товар куплен" : StationInterfaceRules.Reason(reason); }), out _, out _);
            ActionButton("Продать 1 · " + _economy.GetSellPrice(id) + " кр.", () =>
                Facility(() => { bool ok = _facilities.TrySell(id, 1, out string reason); return ok ? "Товар продан" : StationInterfaceRules.Reason(reason); }), out _, out _);
        }
    }
    private void Facility(Func<string> action)
    {
        if (_busy) return; _busy = true;
        try { _message = action(); } finally { _busy = false; _layoutKey = null; Refresh(); }
    }
    private string Costs(StationOperationRuntimeState quote) => quote == null ? "Оплата не настроена" :
        string.Join(" + ", (quote.PaidCosts ?? Array.Empty<StationPaymentCostSnapshot>()).Select(c => c.Amount + " " +
            (c.Resource == StationPaymentResource.Credits ? "кр." : c.Resource == StationPaymentResource.Diamonds ? "алм." :
             _config.GetItemConfigById(c.ItemConfigId)?.DisplayName ?? "астралит")));
    private static string Duration(double seconds) => seconds <= 0 ? "Мгновенно" : seconds.ToString("0") + " сек.";
    private string OfferText(StationActionOffer offer)
    {
        if (offer == null) return "Действие недоступно";
        return StationInterfaceRules.ActionName(offer.Kind) +
            (offer.Kind == StationOperationKind.Upgrade || offer.Kind == StationOperationKind.Construction ? " до уровня " + (offer.Quote?.TargetLevel ?? offer.Level + 1) : "") +
            "\n" + Costs(offer.Quote) + (offer.Quote != null ? " · " + Duration(offer.Quote.DurationSeconds) : "") +
            (offer.Available ? "" : "\n" + StationInterfaceRules.Reason(offer.UnavailableReason));
    }
}
