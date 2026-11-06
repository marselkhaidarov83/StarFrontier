using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StationPaymentModalEditModeTests
{
    private GameObject _root;
    private StationConstructionPaymentPanel2A _modal;
    private Clock _time;
    private Session _session;
    private Cards _cards;
    private StationConfig _station;
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Clock : IGameTimeService
    {
        public GameTimeState State { get; } = new GameTimeState();
        public int CurrentQuantTick => 0;
        public bool IsPaused => State.IsPaused;
        public float SimulationTimeSeconds => 0;
        public float DelayTime => 0;
        public void SetPaused(bool value) { State.IsPaused = value; }
        public void TogglePause() { SetPaused(!IsPaused); }
        public void StepOneDay() { }
        public void WriteTimeToSave(GameRuntimeState state) { }
        public void RestoreTimeFromSave(GameRuntimeState state) { }
        public void Tick(float seconds) { }
    }
    private sealed class Cards : IStationCardService
    {
        public int ConfirmCount, PrepareCount, OrdinaryCost = 10;
        public StationOperationKind LastPreparedKind;
        public bool OrdinaryUnavailable, ReverseOrder;
        public StationCardSnapshot Card = new StationCardSnapshot { Station = new StationRuntimeState { ConfigId = "station", Level = 0 } };
        private StationActionConfirmation _pending;
        private StationOperationKind Kind => Card.Station.IsDestroyed ? StationOperationKind.Recovery :
            Card.Station.Level == 0 ? StationOperationKind.Construction : StationOperationKind.Upgrade;
        private StationActionOffer Offer(string option, StationPaymentResource resource) => new StationActionOffer {
            SystemId = "sys", StationId = "station", Kind = Kind, OptionId = option,
            Quote = new StationOperationRuntimeState { PaymentOptionId = option, TargetLevel = Kind == StationOperationKind.Recovery ? Card.Station.Level : Card.Station.Level + 1, DurationSeconds = Kind == StationOperationKind.Recovery ? 0 : 60,
                PaidCosts = resource == StationPaymentResource.Credits ? new[] {
                    new StationPaymentCostSnapshot { Resource = resource, Amount = OrdinaryCost },
                    new StationPaymentCostSnapshot { Resource = StationPaymentResource.Astralites, Amount = 5, ItemConfigId = "item_astralite_ai_01" } }
                    : new[] { new StationPaymentCostSnapshot { Resource = resource, Amount = 10 } } },
            UnavailableReason = resource == StationPaymentResource.Credits && OrdinaryUnavailable ? "NotEnoughResources" : null };
        public StationCardSnapshot GetCard(string id) => Card;
        public IReadOnlyList<StationActionOffer> GetOffers(string id) => ReverseOrder
            ? new[] { Offer("diamonds", StationPaymentResource.Diamonds), Offer("credits", StationPaymentResource.Credits) }
            : new[] { Offer("credits", StationPaymentResource.Credits), Offer("diamonds", StationPaymentResource.Diamonds) };
        public bool Prepare(string id, StationOperationKind kind, string option, out StationActionConfirmation ticket, out string reason)
        {
            PrepareCount++; LastPreparedKind = kind;
            if (option == "credits" && OrdinaryUnavailable) { _pending = null; ticket = null; reason = "NotEnoughResources"; return false; }
            _pending = new StationActionConfirmation { TicketId = Guid.NewGuid().ToString(), Offer = Offer(option,
                option == "diamonds" ? StationPaymentResource.Diamonds : StationPaymentResource.Credits) };
            ticket = _pending; reason = string.Empty; return true;
        }
        public bool Confirm(string id, out string reason)
        {
            reason = string.Empty;
            if (_pending == null || _pending.TicketId != id) return false;
            if (_pending.Offer.Kind == StationOperationKind.Recovery) Card.Station.IsDestroyed = false;
            _pending = null; ConfirmCount++; return true;
        }
        public void CancelConfirmation() { _pending = null; }
    }
    [SetUp] public void Setup()
    {
        _root = new GameObject("CanvasFixture", typeof(RectTransform), typeof(Canvas));
        _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2200);
        var template = new GameObject("SystemObjectsListPanel", typeof(RectTransform), typeof(Image)); template.transform.SetParent(_root.transform, false);
        template.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 520);
        var header = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI)); header.transform.SetParent(template.transform, false);
        var rows = new GameObject("RowsText", typeof(RectTransform), typeof(TextMeshProUGUI)); rows.transform.SetParent(template.transform, false);
        rows.GetComponent<TMP_Text>().fontSize = 24;
        var background = new GameObject("HeaderBackground", typeof(RectTransform), typeof(Image)); background.transform.SetParent(template.transform, false);
        template.SetActive(false);
        var host = new GameObject("ModalController"); host.transform.SetParent(_root.transform, false);
        _modal = host.AddComponent<StationConstructionPaymentPanel2A>(); _time = new Clock(); _session = new Session(); _cards = new Cards();
        _station = ScriptableObject.CreateInstance<StationConfig>();
        typeof(BaseConfig).GetField("displayName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(_station, "Золотая пристань");
        _cards.Card.Config = _station; _session.State.Player.Credits = 1000; _session.State.Player.Diamonds = 100;
        _session.State.Player.PlayerShipState.ActiveShipId = "ship";
        var ship = new ShipRuntimeData { ShipId = "ship" };
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_ai_01", 1));
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_ancients_01", 2));
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("item_astralite_infected_01", 2));
        _session.State.Player.PlayerShipState.OwnedShips.Add(ship);
        _modal.Initialize(template.transform, _cards, _session, _time, _ => "астралит", _ => { });
    }
    [TearDown] public void Cleanup() { if (_root != null) UnityEngine.Object.DestroyImmediate(_root); if (_station != null) UnityEngine.Object.DestroyImmediate(_station); }
    [TestCase(false)] [TestCase(true)] public void ClosingRestoresOriginalPauseMode(bool wasPaused)
    {
        _time.SetPaused(wasPaused); Assert.That(_modal.Open("sys"), Is.True); Assert.That(_time.IsPaused, Is.True);
        _modal.Close(); Assert.That(_time.IsPaused, Is.EqualTo(wasPaused));
    }
    [Test] public void OpeningTwice_DoesNotReplaceOriginalRunningMode()
    {
        _time.SetPaused(false); _modal.Open("sys"); _modal.Open("sys"); _modal.Close(); Assert.That(_time.IsPaused, Is.False);
    }
    [Test] public void ChoosingThenCancelling_DoesNotConfirmPayment()
    {
        _modal.Open("sys"); _modal.SelectOption(0); Assert.That(_cards.ConfirmCount, Is.Zero);
        _modal.Close(); _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.Zero);
    }
    [Test] public void Confirmation_ClosesModalAndResumesOnlyOnce()
    {
        _time.SetPaused(false); _modal.Open("sys"); _modal.SelectOption(1); _modal.ConfirmPayment(); _modal.ConfirmPayment();
        Assert.That(_cards.ConfirmCount, Is.EqualTo(1)); Assert.That(_modal.IsOpen, Is.False); Assert.That(_time.IsPaused, Is.False);
    }
    [TestCase(false)] [TestCase(true)] public void OnDisable_RestoresOriginalPauseAndClosesModal(bool wasPaused)
    {
        _time.SetPaused(wasPaused); Assert.That(_modal.Open("sys"), Is.True);
        Assert.That(_modal.IsOpen, Is.True); Assert.That(_time.IsPaused, Is.True);
        _modal.enabled = false;
        // Exercise the managed handler without Unity's native Edit Mode message dispatch.
        var onDisable = typeof(StationConstructionPaymentPanel2A).GetMethod("OnDisable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.That(onDisable, Is.Not.Null, "The controller must retain its OnDisable hook.");
        onDisable.Invoke(_modal, null);
        Assert.That(_modal.IsOpen, Is.False); Assert.That(_time.IsPaused, Is.EqualTo(wasPaused));
        Assert.That(_root.transform.Find("StationPaymentModal").gameObject.activeSelf, Is.False);
        _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.Zero);
    }
    [Test] public void ClosingOldModal_DoesNotOverwriteLoadedSessionTime()
    {
        _time.SetPaused(false); _modal.Open("sys"); _session.LoadSession(new GameRuntimeState());
        _time.SetPaused(true); _modal.Close(); Assert.That(_time.IsPaused, Is.True);
    }
    [Test] public void ThreeResourceColumnsFit_OrdinaryUsesTwoAndAcceleratedUsesOne()
    {
        _modal.Open("sys"); var form = _root.transform.Find("StationPaymentModal/StationPaymentPanel").GetComponent<RectTransform>();
        var first = form.Find("PaymentCard_0").GetComponent<RectTransform>(); var second = form.Find("PaymentCard_1").GetComponent<RectTransform>();
        Assert.That(form.rect.width, Is.EqualTo(906).Within(.01)); Assert.That(first.rect.width, Is.EqualTo(576).Within(.01));
        Assert.That(second.rect.width, Is.EqualTo(288).Within(.01));
        Assert.That(second.anchoredPosition.x - first.anchoredPosition.x, Is.EqualTo(586).Within(.01));
        Assert.That(second.anchoredPosition.x + second.rect.width, Is.EqualTo(form.rect.width - 16).Within(.01));
    }
    [Test] public void AlreadyBuiltStation_DoesNotOpenOrPause()
    {
        _time.SetPaused(false); _cards.Card.Station.Level = 1;
        Assert.That(_modal.Open("sys"), Is.False); Assert.That(_time.IsPaused, Is.False);
    }
    private Transform Form => _root.transform.Find("StationPaymentModal/StationPaymentPanel");
    private Button Build => Form.Find("BuildStation").GetComponent<Button>();
    [Test] public void UnaffordableChoice_RemainsSelectedInBlue_AndDisablesPreviouslyEnabledBuild()
    {
        _cards.OrdinaryUnavailable = true; _modal.Open("sys"); _modal.SelectOption(1);
        Assert.That(Build.interactable, Is.True); var activeColor = Build.GetComponent<Image>().color;
        _modal.SelectOption(0);
        Assert.That(Form.Find("PaymentCard_0").GetComponent<Button>().interactable, Is.True);
        Assert.That(Form.Find("PaymentCard_0").GetComponent<Outline>().enabled, Is.True);
        Assert.That(Form.Find("PaymentCard_0/CardHeader").GetComponent<Image>().color, Is.EqualTo(StationPanelStyle2A.PrimaryButtonColor));
        Assert.That(Build.interactable, Is.False); Assert.That(Build.GetComponent<Image>().color, Is.Not.EqualTo(activeColor));
        _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.Zero);
    }
    [Test] public void ReturningToAffordableChoice_ReenablesBuild()
    {
        _cards.OrdinaryUnavailable = true; _modal.Open("sys"); _modal.SelectOption(0); Assert.That(Build.interactable, Is.False);
        _modal.SelectOption(1); Assert.That(Build.interactable, Is.True);
        Assert.That(Build.GetComponent<Image>().color, Is.EqualTo(StationPanelStyle2A.PrimaryButtonColor));
    }
    [Test] public void WalletChangeBeforeClick_InvalidatesPreparedChoice()
    {
        _modal.Open("sys"); _modal.SelectOption(0); _cards.OrdinaryUnavailable = true; _modal.ConfirmPayment();
        Assert.That(Build.interactable, Is.False); Assert.That(_cards.ConfirmCount, Is.Zero);
    }
    [Test] public void PriceChangeBeforeClick_RequiresAnotherSelection()
    {
        _modal.Open("sys"); _modal.SelectOption(0); _cards.OrdinaryCost = 20; _modal.ConfirmPayment();
        Assert.That(Build.interactable, Is.False); Assert.That(_cards.ConfirmCount, Is.Zero);
        _modal.SelectOption(0); _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.EqualTo(1));
    }
    [Test] public void ConfigOrder_DoesNotSwapOrdinaryAndAcceleratedGroups()
    {
        _cards.ReverseOrder = true; _cards.OrdinaryUnavailable = true; _modal.Open("sys"); _modal.SelectOption(0);
        Assert.That(Build.interactable, Is.False); _modal.SelectOption(1); Assert.That(Build.interactable, Is.True);
    }
    [Test] public void PaymentTitle_UsesStationName_AndHasNoCancelButton()
    {
        _modal.Open("sys"); Assert.That(Form.Find("HeaderText").GetComponent<TMP_Text>().text, Is.EqualTo("ЗОЛОТАЯ ПРИСТАНЬ"));
        Assert.That(Form.Find("CancelPayment"), Is.Null);
        Assert.That(Build.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Построить"));
        Assert.That(Form.Find("PaymentCard_0/CardHeader/Title").GetComponent<TMP_Text>().text, Is.EqualTo("Обычная постройка"));
        Assert.That(Form.Find("PaymentCard_1/CardHeader/Title").GetComponent<TMP_Text>().text, Is.EqualTo("Ускоренно"));
    }

    [Test] public void StationTitleAndAdjacentIcon_AreCenteredTogether()
    {
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f));
        try
        {
            typeof(StationConfig).GetField("stationSpriteOverride", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(_station, sprite);
            _modal.Open("sys");
            var title = Form.Find("HeaderText").GetComponent<TMP_Text>(); var icon = Form.Find("HeaderIcon").GetComponent<Image>();
            var a = new Vector3[4]; var b = new Vector3[4]; icon.rectTransform.GetWorldCorners(a); title.rectTransform.GetWorldCorners(b);
            float center = Form.TransformPoint(Form.GetComponent<RectTransform>().rect.center).x;
            Assert.That((a[0].x + b[2].x) * .5f, Is.EqualTo(center).Within(.01));
            Assert.That(b[0].x - a[2].x, Is.EqualTo(8).Within(.01)); Assert.That(icon.sprite, Is.SameAs(sprite));
        }
        finally { UnityEngine.Object.DestroyImmediate(sprite); }
    }

    [TestCase(false)] [TestCase(true)] public void OpeningSelectsOrdinaryPayment_WithoutCommitting(bool unavailable)
    {
        _cards.OrdinaryUnavailable = unavailable; _modal.Open("sys");
        Assert.That(Form.Find("PaymentCard_0").GetComponent<Outline>().enabled, Is.True);
        Assert.That(Form.Find("PaymentCard_1").GetComponent<Outline>().enabled, Is.False);
        Assert.That(Build.interactable, Is.EqualTo(!unavailable)); Assert.That(_cards.ConfirmCount, Is.Zero);
    }
    [Test] public void ReopeningResetsAcceleratedSelectionToOrdinary()
    {
        _modal.Open("sys"); _modal.SelectOption(1); _modal.Close(); _modal.Open("sys");
        Assert.That(Form.Find("PaymentCard_0").GetComponent<Outline>().enabled, Is.True);
        Assert.That(Form.Find("PaymentCard_1").GetComponent<Outline>().enabled, Is.False);
        Assert.That(_cards.ConfirmCount, Is.Zero);
    }

    [Test] public void MixedAstralitePool_IsShownAsSufficientInResourceCard()
    {
        _modal.Open("sys");
        var amount = Form.Find("PaymentCard_0/Resource_Astralites/Amount").GetComponent<TMP_Text>();
        Assert.That(amount.text, Is.EqualTo("5")); Assert.That(amount.color, Is.EqualTo(Color.white));
        Assert.That(Build.interactable, Is.True);
    }

    [TestCase(false)] [TestCase(true)] public void UpgradePayment_PreparesUpgradeAndRestoresPauseAfterSingleConfirmation(bool paused)
    {
        _cards.Card.Station.Level = 2; _time.SetPaused(paused);
        Assert.That(_modal.OpenUpgrade("sys"), Is.True); Assert.That(_time.IsPaused, Is.True);
        Assert.That(_cards.LastPreparedKind, Is.EqualTo(StationOperationKind.Upgrade));
        Assert.That(Build.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Улучшить до уровня 3"));
        Assert.That(Form.Find("PaymentCard_0/CardHeader/Title").GetComponent<TMP_Text>().text, Is.EqualTo("Обычное улучшение"));
        Assert.That(_cards.ConfirmCount, Is.Zero); _modal.ConfirmPayment(); _modal.ConfirmPayment();
        Assert.That(_cards.ConfirmCount, Is.EqualTo(1)); Assert.That(_time.IsPaused, Is.EqualTo(paused));
    }
    [Test] public void UpgradeAtMaximumLevel_DoesNotOpenOrPause()
    {
        _cards.Card.Station.Level = 10; _time.SetPaused(false);
        Assert.That(_modal.OpenUpgrade("sys"), Is.False); Assert.That(_time.IsPaused, Is.False);
    }
    [Test] public void UpgradeLevelChangedDuringPreview_DoesNotCommitOldPayment()
    {
        _cards.Card.Station.Level = 2; _modal.OpenUpgrade("sys"); _cards.Card.Station.Level = 3;
        _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.Zero); Assert.That(_modal.IsOpen, Is.False);
    }


    [TestCase(false)] [TestCase(true)] public void Recovery_UsesSamePaymentFormAndPreservesLevel(bool paused)
    {
        _cards.Card.Station.Level = 5; _cards.Card.Station.IsDestroyed = true; _time.SetPaused(paused);
        Assert.That(_modal.OpenRecovery("sys"), Is.True); Assert.That(_time.IsPaused, Is.True);
        Assert.That(_cards.LastPreparedKind, Is.EqualTo(StationOperationKind.Recovery));
        Assert.That(Build.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Восстановить"));
        Assert.That(Form.Find("PaymentCard_0/CardHeader/Title").GetComponent<TMP_Text>().text,
            Is.EqualTo("Обычное восстановление"));
        Assert.That(Form.Find("PaymentCard_0").GetComponent<Outline>().enabled, Is.True);
        _modal.ConfirmPayment(); _modal.ConfirmPayment();
        Assert.That(_cards.ConfirmCount, Is.EqualTo(1)); Assert.That(_cards.Card.Station.IsDestroyed, Is.False);
        Assert.That(_cards.Card.Station.Level, Is.EqualTo(5)); Assert.That(_time.IsPaused, Is.EqualTo(paused));
    }
    [Test] public void Recovery_AllowsPausedPaidUpgradeWithoutReplacingItsProgress()
    {
        _cards.Card.Station.Level = 3; _cards.Card.Station.IsDestroyed = true;
        var upgrade = new StationOperationRuntimeState { Kind = StationOperationKind.Upgrade, TargetLevel = 4, ElapsedSeconds = 7 };
        _cards.Card.Station.Development = upgrade;
        Assert.That(_modal.OpenRecovery("sys"), Is.True);
        _modal.ConfirmPayment();
        Assert.That(_cards.Card.Station.Development, Is.SameAs(upgrade));
        Assert.That(upgrade.ElapsedSeconds, Is.EqualTo(7)); Assert.That(_cards.Card.Station.Level, Is.EqualTo(3));
    }
    [Test] public void Recovery_UnaffordableChoiceDisablesConfirm_AndDiamondsRemainSelectable()
    {
        _cards.Card.Station.Level = 10; _cards.Card.Station.IsDestroyed = true; _cards.OrdinaryUnavailable = true;
        Assert.That(_modal.OpenRecovery("sys"), Is.True); Assert.That(Build.interactable, Is.False);
        _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.Zero);
        _modal.SelectOption(1); Assert.That(Build.interactable, Is.True);
        _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.EqualTo(1));
        Assert.That(_cards.Card.Station.Level, Is.EqualTo(10));
    }
    [Test] public void Recovery_StateChangedBeforeClick_DoesNotPay()
    {
        _cards.Card.Station.Level = 2; _cards.Card.Station.IsDestroyed = true;
        _time.SetPaused(false); _modal.OpenRecovery("sys"); _cards.Card.Station.IsDestroyed = false;
        _modal.ConfirmPayment(); Assert.That(_cards.ConfirmCount, Is.Zero);
        Assert.That(_modal.IsOpen, Is.False); Assert.That(_time.IsPaused, Is.False);
    }
    [Test] public void Recovery_RejectsOperationalOrAlreadyRecoveringStation()
    {
        _cards.Card.Station.Level = 2; Assert.That(_modal.OpenRecovery("sys"), Is.False);
        _cards.Card.Station.IsDestroyed = true;
        _cards.Card.Station.Recovery = new StationOperationRuntimeState { Kind = StationOperationKind.Recovery };
        Assert.That(_modal.OpenRecovery("sys"), Is.False);
    }

    private sealed class ResearchInfrastructure : IStationInfrastructureService
    {
        public void InitializeSession() { }
        public float GetRangerAttackProbability(string id, out bool configured) { configured = false; return 0; }
        public double GetScienceRate() => 2;
        public bool CanTravel(SystemNpcRuntimeState npc, string id) => false;
        public void AdvanceTime(double seconds) { }
        public double NextRepairBoundary(double maximumSeconds) => maximumSeconds;
        public void CompleteTick(int tick) { }
        public void AdvanceOfflineRepair(double seconds) { }
        public void RelocateStationMilitary() { }
    }
    [TestCase(1, false)] [TestCase(10, false)] [TestCase(1, true)]
    public void Research_ReusesPaymentModal_RestoresPauseAndNeverUpgradesStation(int stationLevel, bool cancel)
    {
        var tree = ScriptableObject.CreateInstance<ResearchTreeConfig>();
        var branch = ScriptableObject.CreateInstance<ResearchBranchConfig>();
        try
        {
            var levels = new ResearchNodeLevel[10];
            for (int i = 0; i < 10; i++) levels[i] = new ResearchNodeLevel { Level = i + 1, PaymentOptions = new[] {
                new StationPaymentOption { Id = "ordinary", DurationSeconds = 60, Costs = new[] {
                    new StationPaymentCost { Resource = StationPaymentResource.Credits, Amount = 10 },
                    new StationPaymentCost { Resource = StationPaymentResource.Astralites, Amount = 2 } } },
                new StationPaymentOption { Id = "diamonds", DurationSeconds = 60, Costs = new[] {
                    new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = 10 } } } } };
            branch.Nodes = new[] { new ResearchNode { Upgrade = ResearchUpgrade.MilitarySuperiority, Title = "Исследование", Levels = levels } };
            tree.Branches = new[] { branch };
            _cards.Card.Station.Level = stationLevel; _cards.Card.Station.StationType = StationType.Science;
            _cards.Card.IsInside = true; _cards.Card.GalaxyLevel = 1;
            _session.State.Player.CurrentSystemId = "sys"; _session.State.Player.CurrentStationId = "station";
            _session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "sys", Station = _cards.Card.Station });
            var infrastructure = new ResearchInfrastructure();
            var research = new ResearchService(tree, _session, null, infrastructure, new StationResourcePaymentGateway(_session), new SimpleEventBus(), _ => true);
            var source = new ResearchPaymentCardService2A(research, _cards, _session, infrastructure, "sys", ResearchUpgrade.MilitarySuperiority);
            _time.SetPaused(false);
            Assert.That(_modal.OpenResearch("sys", source), Is.True); Assert.That(_time.IsPaused, Is.True);
            Assert.That(Build.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Исследовать"));
            Assert.That(Form.Find("PaymentCard_0/CardHeader/Title").GetComponent<TMP_Text>().text, Is.EqualTo("Обычное исследование"));
            if (cancel) _modal.Close(); else { _modal.ConfirmPayment(); _modal.ConfirmPayment(); }
            Assert.That(_session.State.Player.Credits, Is.EqualTo(cancel ? 1000 : 990));
            Assert.That(_cards.Card.Station.Level, Is.EqualTo(stationLevel)); Assert.That(_cards.Card.Station.Development, Is.Null);
            Assert.That(_cards.ConfirmCount, Is.Zero); Assert.That(_time.IsPaused, Is.False);
            Assert.That(research.GetSnapshot().Active != null, Is.EqualTo(!cancel));
        }
        finally { UnityEngine.Object.DestroyImmediate(tree); UnityEngine.Object.DestroyImmediate(branch); }
    }
}
