using System;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SharedRefuelPanelEditModeTests
{
    private GameObject _root;
    private Session _session;
    private Fuel _fuel;
    private SimpleEventBus _events;
    private RefuelPanelController _controller;
    private RefuelPanelView2A _view;
    private Texture2D _texture;
    private Sprite _planet, _station;
    private ShipRuntimeData Ship => _session.State.Player.GetActiveShip();
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Fuel : IRefuelService
    {
        public Session Session; public SimpleEventBus Events;
        public int Price = 5, Calls, LastAmount, PriceReads; public Action DuringPurchase;
        private ShipRuntimeData Ship => Session.State.Player.GetActiveShip();
        public int GetCurrentFuel() => Ship.CurrentFuel;
        public int GetFuelCapacity() => Ship.FuelCapacity;
        public int GetFuelUnitPrice() { PriceReads++; return Price; }
        public bool CanRefuel(int amount) => amount > 0 && Price > 0 &&
            Ship.CurrentFuel + amount <= Ship.FuelCapacity && (long)Price * amount <= Session.State.Player.Credits;
        public RefuelResult Refuel(int amount)
        {
            Calls++; LastAmount = amount; DuringPurchase?.Invoke();
            if (!CanRefuel(amount)) return RefuelResult.Create(RefuelResultType.NotEnoughCredits, 0, 0, Ship.CurrentFuel, Ship.FuelCapacity);
            Ship.CurrentFuel += amount; Session.State.Player.Credits -= amount * Price;
            Events.Publish(new FuelChangedEvent(Ship.CurrentFuel));
            Events.Publish(new CreditsChangedEvent(Session.State.Player.Credits));
            Events.Publish(new SaveNeedEvent());
            return RefuelResult.Create(RefuelResultType.Success, amount, amount * Price, Ship.CurrentFuel, Ship.FuelCapacity);
        }
        public RefuelResult RefuelToFull() => Refuel(Ship.FuelCapacity - Ship.CurrentFuel);
        public bool CanConsume(int count, out FuelConsumeFailReason reason) { reason = default; return false; }
        public bool Consume(int count, out FuelConsumeFailReason reason) { reason = default; return false; }
    }
    [SetUp] public void Setup()
    {
        _session = new Session(); _events = new SimpleEventBus();
        _session.State.Player.Credits = 1000;
        _session.State.Player.CurrentSystemId = "system"; _session.State.Player.CurrentPlanetId = "planet";
        _session.State.Player.PlayerShipState.ActiveShipId = "ship";
        _session.State.Player.PlayerShipState.OwnedShips.Add(new ShipRuntimeData {
            ShipId = "ship", FuelCapacity = 100, CurrentFuel = 35, CurrentHull = 100 });
        _fuel = new Fuel { Session = _session, Events = _events };
        _texture = new Texture2D(18, 8);
        _planet = Sprite.Create(_texture, new Rect(0, 0, 18, 8), Vector2.one * .5f);
        _station = Sprite.Create(_texture, new Rect(0, 0, 18, 8), Vector2.one * .5f);
        _root = new GameObject("FuelBlock", typeof(RectTransform));
        _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2200);
        _controller = _root.AddComponent<RefuelPanelController>();
        _controller.Initialize(_fuel, _session, _events, () => _planet,
            () => string.IsNullOrEmpty(_session.State.Player.CurrentStationId) ? _planet : _station);
        _view = _root.GetComponent<RefuelPanelView2A>();
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(_root);
        UnityEngine.Object.DestroyImmediate(_planet); UnityEngine.Object.DestroyImmediate(_station);
        UnityEngine.Object.DestroyImmediate(_texture);
    }
    [Test] public void VerticalSlider_UsesFullScaleButClampsToExistingFuel()
    {
        Assert.That(_view.Slider.direction, Is.EqualTo(Slider.Direction.BottomToTop));
        Assert.That(_view.Slider.minValue, Is.Zero); Assert.That(_view.Slider.maxValue, Is.EqualTo(100));
        Assert.That(_view.Slider.value, Is.EqualTo(100));
        _view.Slider.value = 0;
        Assert.That(_view.Slider.value, Is.EqualTo(35));
        Assert.That(_view.FuelOverlay.fillAmount, Is.EqualTo(.35f).Within(.001));
        _view.Slider.value = 100;
        Assert.That(_view.FuelOverlay.fillAmount, Is.EqualTo(1));
        Assert.That(_view.Amount.text, Is.EqualTo("Добавить: 65 ед."));
        Assert.That(_view.Cost.text, Is.EqualTo("325 кр."));
    }
    [Test] public void Refuel_BuysDifferenceOnly_AndPreventsRepeatedCharge()
    {
        int saves = 0; _events.Subscribe<SaveNeedEvent>(evt => saves++);
        _view.Slider.value = 60;
        _fuel.DuringPurchase = () => _view.Refuel.onClick.Invoke();
        _view.Refuel.onClick.Invoke(); _view.Refuel.onClick.Invoke();
        Assert.That(_fuel.Calls, Is.EqualTo(1)); Assert.That(_fuel.LastAmount, Is.EqualTo(25));
        Assert.That(Ship.CurrentFuel, Is.EqualTo(60)); Assert.That(_session.State.Player.Credits, Is.EqualTo(875));
        Assert.That(saves, Is.EqualTo(1)); Assert.That(_view.Refuel.interactable, Is.False);
        Assert.That(_view.Slider.value, Is.EqualTo(60));
    }
    [Test] public void PlanetAndStation_UseTheSameView_AndLocationHeader()
    {
        Assert.That(_view.Header.sprite, Is.SameAs(_planet));
        _session.State.Player.CurrentPlanetId = ""; _session.State.Player.CurrentStationId = "station";
        _events.Publish(new RefuelEnteredEvent());
        Assert.That(_root.GetComponents<RefuelPanelView2A>().Length, Is.EqualTo(1));
        Assert.That(_view.Header.sprite, Is.SameAs(_station));
        _view.Slider.value = 40; _view.Refuel.onClick.Invoke();
        Assert.That(_fuel.LastAmount, Is.EqualTo(5));
        _session.State.Player.CurrentStationId = ""; _session.State.Player.CurrentPlanetId = "planet";
        _events.Publish(new RefuelEnteredEvent());
        Assert.That(_view.Header.sprite, Is.SameAs(_planet));
        Assert.That(_root.GetComponent<RefuelPanelView2A>(), Is.SameAs(_view));
    }
    [Test] public void WorkingArea_StartsBelowIllustration_AndLeavesBottomHud()
    {
        _view.RefreshLayout();
        Assert.That(_view.Header.rectTransform.anchoredPosition.y, Is.EqualTo(-194));
        Assert.That(_view.Header.rectTransform.rect.height, Is.EqualTo(640).Within(.01));
        var workspace = _view.TankColumn.parent as RectTransform;
        Assert.That(workspace.offsetMax.y, Is.EqualTo(-834).Within(.01));
        Assert.That(workspace.offsetMin.y, Is.EqualTo(142));
        Assert.That(_view.TankColumn.childCount, Is.Zero);
        foreach (var text in _root.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(text.text, Is.Not.EqualTo("ЗАПРАВКА"));
            Assert.That(text.text, Is.Not.EqualTo("Корабль"));
            Assert.That(text.text, Is.Not.EqualTo("Баки корабля"));
            Assert.That(text.text, Does.Not.StartWith("Сейчас"));
        }
    }
    [Test] public void CreditsChange_DisablesUnaffordablePurchaseWithoutChangingSelectedTarget()
    {
        _view.Slider.value = 95; _session.State.Player.Credits = 0;
        _events.Publish(new CreditsChangedEvent(0));
        Assert.That(_view.Slider.value, Is.EqualTo(95)); Assert.That(_view.Refuel.interactable, Is.False);
        Assert.That(_view.Warning.text, Is.EqualTo("Недостаточно кредитов"));
        _view.Refuel.onClick.Invoke(); Assert.That(_fuel.Calls, Is.Zero);
    }
    [Test] public void FullShip_DisablesSliderAndPurchase()
    {
        Ship.CurrentFuel = 100; _events.Publish(new FuelChangedEvent(100));
        Assert.That(_view.Slider.value, Is.EqualTo(100)); Assert.That(_view.Slider.interactable, Is.False);
        Assert.That(_view.Refuel.interactable, Is.False); Assert.That(_view.Warning.text, Is.EqualTo("Бак заполнен"));
    }
    [Test] public void PriceChange_RejectsStaleClickWithoutCharge()
    {
        _view.Slider.value = 65; _fuel.Price = 8; _view.Refuel.onClick.Invoke();
        Assert.That(_fuel.Calls, Is.Zero); Assert.That(Ship.CurrentFuel, Is.EqualTo(35));
        Assert.That(_view.Warning.text, Does.Contain("изменились"));
    }
    [Test] public void ChangedShip_RejectsOldSelection_AndResetsToNewFuel()
    {
        _view.Slider.value = 65;
        _session.State.Player.PlayerShipState.OwnedShips.Add(new ShipRuntimeData { ShipId = "other", CurrentFuel = 10, FuelCapacity = 80 });
        _session.State.Player.PlayerShipState.ActiveShipId = "other";
        _view.Refuel.onClick.Invoke();
        Assert.That(_fuel.Calls, Is.Zero); Assert.That(_view.Slider.value, Is.EqualTo(10));
        Assert.That(_view.Slider.maxValue, Is.EqualTo(80));
    }
    [Test] public void ChangedLocation_RejectsStaleSelection()
    {
        _view.Slider.value = 65; _session.State.Player.CurrentPlanetId = "other";
        _view.Refuel.onClick.Invoke(); Assert.That(_fuel.Calls, Is.Zero);
        Assert.That(_view.Slider.value, Is.EqualTo(35));
    }
    [Test] public void Reinitializing_DoesNotDuplicateControlsOrPaymentListeners()
    {
        _controller.Initialize(_fuel, _session, _events, () => _planet, () => _planet);
        Assert.That(_root.GetComponentsInChildren<Slider>(true).Length, Is.EqualTo(1));
        _view.Slider.value = 36; _view.Refuel.onClick.Invoke(); Assert.That(_fuel.Calls, Is.EqualTo(1));
    }
    [Test] public void SwitchingToSmallerCapacity_DoesNotKeepAReentrantSliderSelection()
    {
        _view.Slider.value = 100;
        _session.State.Player.PlayerShipState.OwnedShips.Add(new ShipRuntimeData { ShipId = "small", CurrentFuel = 5, FuelCapacity = 20 });
        _session.State.Player.PlayerShipState.ActiveShipId = "small";
        _events.Publish(new ActiveShipChangedEvent("small"));
        Assert.That(_view.Slider.value, Is.EqualTo(5));
        Assert.That(_view.Amount.text, Is.EqualTo("Добавить: 0 ед."));
        Assert.That(_view.Refuel.interactable, Is.False);
        _view.Slider.value = 7; _view.Refuel.onClick.Invoke();
        Assert.That(_fuel.LastAmount, Is.EqualTo(2));
    }

    [Test] public void FuelFill_HasNoDefaultSizeRectangleAtZeroOrMaximum()
    {
        Ship.CurrentFuel = 0; _events.Publish(new FuelChangedEvent(0)); _view.RefreshLayout();
        Assert.That(_view.Slider.fillRect.sizeDelta, Is.EqualTo(Vector2.zero));
        Assert.That(_view.Slider.fillRect.rect.height, Is.EqualTo(0).Within(.01));
        Assert.That(_view.Slider.handleRect.sizeDelta, Is.EqualTo(new Vector2(36, 36)));
        _view.Slider.value = 100;
        var area = _view.Slider.fillRect.parent as RectTransform;
        Assert.That(_view.Slider.fillRect.rect.height, Is.EqualTo(area.rect.height).Within(.01));
        Assert.That(_view.Slider.fillRect.rect.width, Is.EqualTo(area.rect.width).Within(.01));
    }
    [Test] public void RefuelCards_UseMissionPaletteFrameAndGutters()
    {
        var left = _view.Slider.transform.parent.GetComponent<Image>();
        var right = _view.TankColumn.GetComponent<Image>();
        foreach (var image in new[] { left, right })
        {
            Assert.That(image.color, Is.EqualTo(MissionPanelStyle2A.CardBackground));
            Assert.That(image.GetComponent<Outline>().effectColor, Is.EqualTo(MissionPanelStyle2A.CardAccent));
            Assert.That(image.GetComponent<Outline>().effectDistance, Is.EqualTo(new Vector2(2, 2)));
        }
        Assert.That(left.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(18, -18)));
        Assert.That(_view.TankColumn.anchoredPosition.x, Is.GreaterThan(left.rectTransform.rect.width + 18));
        _view.Slider.value = 50;
        Assert.That(_view.Refuel.GetComponent<Image>().color, Is.EqualTo(MissionPanelStyle2A.ActionBackground));
        Assert.That(_view.Refuel.GetComponent<Outline>().effectColor, Is.EqualTo(MissionPanelStyle2A.ActionAccent));
    }

    [Test] public void EmptyWarning_DoesNotLeaveReservedSpaceBelowRefuelButton()
    {
        _view.SetWarning(string.Empty); _view.RefreshLayout();
        var button = _view.Refuel.GetComponent<RectTransform>();
        var card = button.parent as RectTransform;
        float bottomGap = card.rect.height + button.anchoredPosition.y - button.rect.height;
        Assert.That(bottomGap, Is.EqualTo(18f).Within(.01f));
        Assert.That(_view.Warning.gameObject.activeSelf, Is.False);
        _view.SetWarning("Недостаточно кредитов");
        float warningGap = card.rect.height + button.anchoredPosition.y - button.rect.height;
        Assert.That(warningGap, Is.EqualTo(78f).Within(.01f));
        Assert.That(_view.Warning.gameObject.activeSelf, Is.True);
        _view.SetWarning(string.Empty);
        Assert.That(card.rect.height + button.anchoredPosition.y - button.rect.height, Is.EqualTo(18f).Within(.01f));
    }
    [Test] public void FuelPreview_UsesAlphaSilhouetteMaterialAndVerticalProgress()
    {
        var material = Resources.Load<Material>("UIFuelSilhouette2A");
        Assert.That(material, Is.Not.Null);
        Assert.That(material.shader.name, Is.EqualTo("StarFrontier/UI/FuelSilhouette2A"));
        Assert.That(_view.FuelOverlay.material, Is.SameAs(material));
        Assert.That(material.HasProperty("_StencilComp"), Is.True);
        Assert.That(material.HasProperty("_ClipRect"), Is.True);
        Assert.That(_view.FuelOverlay.sprite, Is.SameAs(_view.Ship.sprite));
        Assert.That(_view.FuelOverlay.color.g, Is.GreaterThan(_view.FuelOverlay.color.r));
        Assert.That(_view.FuelOverlay.color.a, Is.InRange(.25f, .4f));
        Assert.That(_view.FuelOverlay.fillMethod, Is.EqualTo(Image.FillMethod.Vertical));
        Assert.That(_view.FuelOverlay.fillOrigin, Is.EqualTo((int)Image.OriginVertical.Bottom));
        _view.Slider.value = 50;
        Assert.That(_view.FuelOverlay.fillAmount, Is.EqualTo(.5f).Within(.001));
        _view.Slider.value = 100;
        Assert.That(_view.FuelOverlay.fillAmount, Is.EqualTo(1f));
    }

    private static void Lifecycle(RefuelPanelController controller, string method)
    {
        // Direct managed call; Unity SendMessage is not valid for these EditMode tests.
        typeof(RefuelPanelController).GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
    }
    [Test] public void HiddenPanel_UnsubscribesAndDoesNotReadFuelDuringTravel()
    {
        _root.SetActive(false); Lifecycle(_controller, "OnDisable");
        int reads = _fuel.PriceReads;
        Ship.CurrentFuel = 12;
        Assert.DoesNotThrow(() => _events.Publish(new FuelChangedEvent(12)));
        _events.Publish(new CreditsChangedEvent(500));
        _events.Publish(new ActiveShipChangedEvent("ship"));
        Assert.That(_fuel.PriceReads, Is.EqualTo(reads));
        _root.SetActive(true); Lifecycle(_controller, "OnEnable");
        Assert.That(_view.Slider.value, Is.EqualTo(100));
        reads = _fuel.PriceReads;
        _events.Publish(new FuelChangedEvent(12));
        Assert.That(_fuel.PriceReads, Is.EqualTo(reads + 1), "Reopening must not duplicate subscriptions");
    }
    [Test] public void NeverShownPanel_DoesNotSubscribeDuringInitialization()
    {
        var hidden = new GameObject("NeverOpenedFuelBlock", typeof(RectTransform));
        hidden.SetActive(false);
        try
        {
            var controller = hidden.AddComponent<RefuelPanelController>();
            controller.Initialize(_fuel, _session, _events, () => _planet, () => _planet);
            Lifecycle(_controller, "OnDisable");
            int reads = _fuel.PriceReads;
            _events.Publish(new FuelChangedEvent(20));
            Assert.That(_fuel.PriceReads, Is.EqualTo(reads));
        }
        finally { UnityEngine.Object.DestroyImmediate(hidden); }
    }
    [Test] public void DestroyedPanel_DoesNotInterruptRemainingFuelObservers()
    {
        var retained = _controller;
        UnityEngine.Object.DestroyImmediate(_root); _root = null;
        bool reached = false; _events.Subscribe<FuelChangedEvent>(evt => reached = true);
        Assert.DoesNotThrow(() => _events.Publish(new FuelChangedEvent(5)));
        Assert.DoesNotThrow(() => retained.Refresh());
        Assert.That(reached, Is.True);
    }
    [Test] public void ImageDestroyedDuringEventDispatch_DoesNotThrowFromCapturedSubscriber()
    {
        Lifecycle(_controller, "OnDisable");
        _events.Subscribe<FuelChangedEvent>(evt => UnityEngine.Object.DestroyImmediate(_view.Header.gameObject));
        Lifecycle(_controller, "OnEnable");
        bool reached = false; _events.Subscribe<FuelChangedEvent>(evt => reached = true);
        Assert.DoesNotThrow(() => _events.Publish(new FuelChangedEvent(10)));
        Assert.That(_view.IsReady, Is.False); Assert.That(reached, Is.True);
        Assert.DoesNotThrow(() => _view.SetSprites(_planet, _planet));
        Assert.DoesNotThrow(() => _view.SetPreview(10, 30, 100, 100, true, string.Empty));
        Assert.DoesNotThrow(() => _view.RefreshLayout());
    }

    [TestCase(false)] [TestCase(true)]
    public void OpeningRefuel_DefaultsToMaximumWithoutBuying_AndCreditUpdatesKeepManualChoice(bool station)
    {
        _session.State.Player.CurrentPlanetId = station ? "" : "planet";
        _session.State.Player.CurrentStationId = station ? "station" : "";
        _view.Slider.value = 50;
        _events.Publish(new RefuelEnteredEvent());
        Assert.That(_view.Slider.value, Is.EqualTo(100));
        Assert.That(_view.Amount.text, Is.EqualTo("Добавить: 65 ед."));
        Assert.That(_view.Cost.text, Is.EqualTo("325 кр."));
        Assert.That(Ship.CurrentFuel, Is.EqualTo(35)); Assert.That(_fuel.Calls, Is.Zero);
        _view.Slider.value = 70;
        _events.Publish(new CreditsChangedEvent(1000));
        Assert.That(_view.Slider.value, Is.EqualTo(70));
    }
}
