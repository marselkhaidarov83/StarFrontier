using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Docked station research screen. Closing it never calls LeaveStation or changes shared research.</summary>
public sealed class StationResearchPanelController : MonoBehaviour
{
    private IResearchService _research;
    private IGameSessionService _session;
    private IStationCardService _cards;
    private IStationInfrastructureService _infrastructure;
    private IConfigService _config;
    private IGameTimeService _time;
    private SimpleEventBus _events;
    private StationResearchPanelView2A _view;
    private StationConstructionPaymentPanel2A _payment;
    private GameRuntimeState _boundSession;
    private ResearchBranchConfig[] _branches = Array.Empty<ResearchBranchConfig>();
    private ResearchNode[] _nodes = Array.Empty<ResearchNode>();
    private ResearchRuntimeState _snapshot;
    private int _selected;
    private string _systemId, _stationId, _message = string.Empty;
    private bool _initialized, _subscribed, _refreshing, _busy;
    private float _nextRefresh, _nextCommandAt;
    private double _scienceRate;
    public void Initialize(TMP_Text template, IResearchService research, IGameSessionService session,
        IStationCardService cards, IStationInfrastructureService infrastructure, IConfigService config,
        IGameTimeService time, SimpleEventBus events)
    {
        _research = research; _session = session; _cards = cards; _infrastructure = infrastructure;
        _config = config; _time = time; _events = events;
        _view = GetComponent<StationResearchPanelView2A>() ?? gameObject.AddComponent<StationResearchPanelView2A>();
        _view.Build(template); _view.Upgrade.onClick.AddListener(OpenUpgrade);
        _initialized = true;
    }
    public bool TryOpen()
    {
        var player = _session?.State?.Player;
        if (!_initialized || player == null || !_research.TryEnterStation(player.CurrentSystemId, out string reason))
        { AppLog.Warning(DebugLogChannel.Research, "[ResearchUI] Open rejected: station docking required"); return false; }
        _boundSession = _session.State; _systemId = player.CurrentSystemId; _stationId = player.CurrentStationId;
        _message = string.Empty; gameObject.SetActive(true); Subscribe(); Refresh();
        AppLog.Info(DebugLogChannel.Research, "[ResearchUI] Opened | System=" + _systemId); return true;
    }
    private bool CanUse => _initialized && isActiveAndEnabled && _view != null && _view.IsReady;
    private bool HasAccess()
    {
        var state = _session?.State; var player = state?.Player;
        return player != null && ReferenceEquals(_boundSession, state) && player.CurrentSystemId == _systemId &&
            player.CurrentStationId == _stationId && _research.TryEnterStation(_systemId, out _);
    }
    private void OnEnable() { if (_initialized) Subscribe(); }
    private void OnDisable()
    {
        Unsubscribe(); if (_payment != null) _payment.Close();
        _boundSession = null;
    }
    private void OnDestroy()
    {
        Unsubscribe(); if (_view != null && _view.Upgrade != null) _view.Upgrade.onClick.RemoveListener(OpenUpgrade);
    }
    private void Subscribe()
    {
        if (_subscribed || _events == null || !CanUse) return;
        _events.Subscribe<StationStateChangedEvent>(OnStation);
        _events.Subscribe<ResearchCompletedEvent>(OnResearch);
        _events.Subscribe<CreditsChangedEvent>(OnCredits);
        _events.Subscribe<DiamondsChangedEvent>(OnDiamonds);
        _events.Subscribe<CargoChangedEvent>(OnCargo); _subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!_subscribed || _events == null) return;
        _events.Unsubscribe<StationStateChangedEvent>(OnStation); _events.Unsubscribe<ResearchCompletedEvent>(OnResearch);
        _events.Unsubscribe<CreditsChangedEvent>(OnCredits); _events.Unsubscribe<DiamondsChangedEvent>(OnDiamonds);
        _events.Unsubscribe<CargoChangedEvent>(OnCargo); _subscribed = false;
    }
    private void OnStation(StationStateChangedEvent evt) { if (evt.SystemId == _systemId) Refresh(); }
    private void OnResearch(ResearchCompletedEvent evt) => Refresh();
    private void OnCredits(CreditsChangedEvent evt) => Refresh();
    private void OnDiamonds(DiamondsChangedEvent evt) => Refresh();
    private void OnCargo(CargoChangedEvent evt) => Refresh();
    private void Update()
    {
        if (!CanUse || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .2f; Refresh();
    }
    private void SelectBranch(int index)
    {
        if (index < 0 || index >= _branches.Length || _busy) return;
        _selected = index; _message = string.Empty; _view.ClearCards(); Refresh(); _view.ResetTreeScroll();
        AppLog.Info(DebugLogChannel.Research, "[ResearchUI] Group selected | Id=" + _branches[index].Id);
    }
    public void Refresh()
    {
        if (!CanUse || _refreshing || _busy) return;
        _refreshing = true;
        try
        {
            if (!HasAccess()) { gameObject.SetActive(false); return; }
            var card = _cards.GetCard(_systemId);
            if (card == null) { gameObject.SetActive(false); return; }
            _view.SetStation(card, card.Config.StationTypeConfig?.MissionIllustration);
            _branches = (_research.Tree?.Branches ?? Array.Empty<ResearchBranchConfig>()).Where(b => b != null && (card.Station.StationType == StationType.Science || b.StationType == card.Station.StationType)).ToArray();
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _branches.Length - 1));
            _view.SetGroups(_branches, _selected, SelectBranch, card.Station.StationType == StationType.Science);
            _snapshot = _research.GetSnapshot();
            if (_snapshot == null) return;
            var branch = _branches.Length > 0 ? _branches[_selected] : null;
            _nodes = ResearchRules.VisibleNodes(branch, card.GalaxyLevel).ToArray();
            string notice = _message;
            if (string.IsNullOrEmpty(notice) && _nodes.Length == 0) notice = "Нет доступных исследований";
            _view.SetHeading(branch?.DisplayName ?? "Исследования", card.GalaxyLevel, notice);
            _scienceRate = _infrastructure.GetScienceRate();
            var models = _nodes.Select(node => Describe(node, card.GalaxyLevel, branch)).ToArray();
            var boundNodes = _nodes; long boundRevision = _snapshot.Revision; var boundSession = _boundSession;
            _view.SetCards(models, (index, option) => {
                if (index >= 0 && index < boundNodes.Length)
                    OpenResearchPayment(boundNodes[index].Upgrade, boundRevision, boundSession);
            });
        }
        finally { _refreshing = false; }
    }
    private ResearchCardData2A Describe(ResearchNode node, int galaxyLevel, ResearchBranchConfig branch)
    {
        int level = _snapshot.GetLevel(node.Upgrade);
        bool running = _snapshot.Active != null && _snapshot.ActiveUpgrade == node.Upgrade;
        var model = new ResearchCardData2A { Title = node.Title, Level = level, Completed = level >= 10,
            Running = running, Icon = node.Icon != null ? node.Icon : branch?.Icon };
        if (model.Completed) { model.Condition = "Завершено"; return model; }
        if (running)
        {
            var op = _snapshot.Active;
            double rate = _scienceRate;
            model.Progress = op.DurationSeconds <= 0 ? 1 : (float)(op.ElapsedSeconds / op.DurationSeconds);
            model.TargetLevel = op.TargetLevel;
            model.Condition = string.Empty;
            model.ProgressText = rate > 0 ? "Осталось " + ResearchPanelUi2A.Time((op.DurationSeconds - op.ElapsedSeconds) / rate) : "Приостановлено: нет научного вклада";
            return model;
        }
        // A visible researched node keeps its card, but has no research buttons when its next level is gated.
        if (!ResearchRules.IsLevelVisible(branch, node, level + 1, galaxyLevel))
        { model.Locked = true; model.Condition = "Следующий уровень пока недоступен"; return model; }
        var missing = ResearchRules.DependenciesFor(node, level + 1).FirstOrDefault(d => d != null && _snapshot.GetLevel(d.Upgrade) < d.RequiredLevel);
        if (missing != null)
        {
            model.Locked = true;
            model.Condition = "Требуется «" + (ResearchRules.Find(_research.Tree, missing.Upgrade)?.Title ?? missing.Upgrade.ToString()) + "», уровень " + missing.RequiredLevel;
            return model;
        }
        if (_snapshot.Active != null) { model.Condition = "В галактике уже выполняется исследование"; return model; }
        var next = node.Levels?.FirstOrDefault(l => l != null && l.Level == level + 1);
        model.Condition = string.Empty;
        if (next?.PaymentOptions?.Any(o => o != null) == true)
            model.Choices = new[] { new ResearchPaymentChoice2A { OptionId = string.Empty, Label = "Исследовать",
                Available = Time.unscaledTime >= _nextCommandAt && _scienceRate > 0 } };
        if (_scienceRate <= 0) model.Condition = "Нет действующего научного вклада";
        return model;
    }
    private void OpenResearchPayment(ResearchUpgrade upgrade, long revision, GameRuntimeState session)
    {
        if (!CanUse || _busy || Time.unscaledTime < _nextCommandAt || !HasAccess() || !ReferenceEquals(session, _session.State)) return;
        if (_research.GetSnapshot()?.Revision != revision) { _message = StationInterfaceRules.Reason("ResearchStateChanged"); Refresh(); return; }
        if (!EnsurePayment()) return;
        _busy = true; _nextCommandAt = Time.unscaledTime + .4f;
        try
        {
            var source = new ResearchPaymentCardService2A(_research, _cards, _session, _infrastructure, _systemId, upgrade);
            bool opened = _payment.OpenResearch(_systemId, source);
            if (!opened) _message = "Оплата исследования недоступна";
            AppLog.Info(DebugLogChannel.Research, "[ResearchUI] Payment opened | Upgrade=" + upgrade + " | Success=" + opened);
        }
        finally { _busy = false; }
        Refresh();
    }
    private void OpenUpgrade()
    {
        if (!CanUse || !HasAccess() || !EnsurePayment()) return;
        _payment.OpenUpgrade(_systemId);
    }
    private bool EnsurePayment()
    {
        if (_payment == null)
        {
            var objects = FindFirstObjectByType<SystemObjectsListHud2A>(FindObjectsInactive.Include);
            var template = objects != null ? objects.transform.Find("SystemObjectsListPanel") : null;
            if (template == null) { _message = "Панель оплаты недоступна"; Refresh(); return false; }
            _payment = gameObject.AddComponent<StationConstructionPaymentPanel2A>();
            _payment.Initialize(template, _cards, _session, _time, id => _config.GetItemConfigById(id)?.DisplayName,
                message => { _message = string.Empty; Refresh(); },
                resource => resource == StationPaymentResource.Credits ? _config.GetItemConfigById("item_credits_01")?.Icon :
                    resource == StationPaymentResource.Diamonds ? _config.GetItemConfigById("item_diamonds_01")?.Icon : Resources.Load<Sprite>("icon_astralite_generic_01"));
        }
        return true;
    }
}
