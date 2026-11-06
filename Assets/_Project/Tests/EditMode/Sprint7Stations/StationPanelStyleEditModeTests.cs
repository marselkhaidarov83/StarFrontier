using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StationPanelStyleEditModeTests
{
    private GameObject _root;
    [TearDown] public void Cleanup() { if (_root != null) Object.DestroyImmediate(_root); }
    [Test] public void PanelClone_UsesSourceBackgroundAndTopLeftAnchor()
    {
        _root = new GameObject("CanvasFixture", typeof(RectTransform));
        var source = new GameObject("SystemObjectsListPanel", typeof(RectTransform), typeof(Image)); source.transform.SetParent(_root.transform, false);
        source.GetComponent<Image>().color = new Color(.12f, .2f, .3f, .8f);
        source.GetComponent<RectTransform>().anchoredPosition = new Vector2(15, -80);
        source.SetActive(false);
        var clone = StationPanelStyle2A.Clone(source.transform);
        Assert.That(clone.GetComponent<Image>().color, Is.EqualTo(source.GetComponent<Image>().color));
        Assert.That(clone.GetComponent<RectTransform>().anchorMin, Is.EqualTo(Vector2.up));
        Assert.That(clone.GetComponent<RectTransform>().anchorMax, Is.EqualTo(Vector2.up));
        Assert.That(clone.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(15, -80)));
        Assert.That(clone.activeSelf, Is.False);
    }
    [Test] public void Typography_CopiesFontColorSizeAndStyle()
    {
        _root = new GameObject("Fixture", typeof(RectTransform));
        var a = new GameObject("Source", typeof(RectTransform), typeof(TextMeshProUGUI)); a.transform.SetParent(_root.transform);
        var b = new GameObject("Target", typeof(RectTransform), typeof(TextMeshProUGUI)); b.transform.SetParent(_root.transform);
        var source = a.GetComponent<TMP_Text>(); var target = b.GetComponent<TMP_Text>();
        source.fontSize = 27; source.fontStyle = FontStyles.Bold; source.color = new Color(.4f, .6f, .8f);
        StationPanelStyle2A.CopyTypography(source, target);
        Assert.That(target.font, Is.SameAs(source.font)); Assert.That(target.fontSharedMaterial, Is.SameAs(source.fontSharedMaterial));
        Assert.That(target.fontSize, Is.EqualTo(27)); Assert.That(target.color, Is.EqualTo(source.color)); Assert.That(target.fontStyle, Is.EqualTo(FontStyles.Bold));
    }
    [Test] public void InterfaceLogging_IsOffByDefault()
    {
        var config = ScriptableObject.CreateInstance<DebugLogConfig>();
        try { Assert.That(config.IsEnabled(DebugLogChannel.StationInterface), Is.False); }
        finally { Object.DestroyImmediate(config); }
    }
    [TestCase(StationType.Military, "Военная база")]
    [TestCase(StationType.RangerBase, "База рейнджеров")]
    [TestCase(StationType.Trade, "Торговая станция")]
    [TestCase(StationType.Science, "Научная станция")]
    [TestCase(StationType.Medical, "Медицинская станция")]
    public void UnbuiltStation_ShowsOnlyTypeAndWaitingState(StationType type, string title)
    {
        _root = new GameObject("StationPanelFixture");
        var panel = _root.AddComponent<SystemStationPanel2A>();
        var card = new StationCardSnapshot { Station = new StationRuntimeState { Level = 0, StationType = type } };
        var describe = typeof(SystemStationPanel2A).GetMethod("Describe", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(describe.Invoke(panel, new object[] { card }), Is.EqualTo("Тип станции: " + title + "\n<color=#AAB7C4>Состояние: ожидает постройки</color>" +
            (type == StationType.Science ? "\nСила исследований: 0 / 0" : type == StationType.Military ? "\nПопуляция военных: 0 / 0" : string.Empty)));
    }

    [Test] public void ConstructionButton_IsOutsideScrollMaskAndInsideBottomOfPanel()
    {
        _root = new GameObject("StationPanel", typeof(RectTransform));
        var panel = _root.GetComponent<RectTransform>(); panel.sizeDelta = new Vector2(400, 260);
        var content = StationPanelStyle2A.ScrollContent(panel, Color.black);
        var footer = StationPanelStyle2A.ConstructionFooter(panel, null, Color.black, () => { });
        footer.gameObject.SetActive(true);
        Assert.That(footer.transform.parent, Is.SameAs(panel));
        Assert.That(footer.GetComponentInParent<RectMask2D>(), Is.Null);
        Assert.That(content.GetComponentInParent<RectMask2D>(), Is.Not.Null);
        var corners = new Vector3[4]; footer.GetComponent<RectTransform>().GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var local = panel.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(panel.rect.xMin, panel.rect.xMax));
            Assert.That(local.y, Is.InRange(panel.rect.yMin, panel.rect.yMax));
        }
        var label = footer.GetComponentInChildren<TMP_Text>();
        Assert.That(label.text, Is.EqualTo("Построить")); Assert.That(label.color, Is.EqualTo(Color.white));
        Assert.That(footer.GetComponent<Image>().color, Is.EqualTo(StationPanelStyle2A.PrimaryButtonColor));
    }
    [Test] public void ConstructionButton_ClickInvokesActionOnce()
    {
        _root = new GameObject("StationPanel", typeof(RectTransform));
        int clicks = 0;
        var footer = StationPanelStyle2A.ConstructionFooter(_root.GetComponent<RectTransform>(), null, Color.black, () => clicks++);
        footer.gameObject.SetActive(true); footer.onClick.Invoke();
        Assert.That(clicks, Is.EqualTo(1));
    }

    private RectTransform CreatePanelUnderShortHud(float canvasHeight)
    {
        _root = new GameObject("CanvasFixture", typeof(RectTransform), typeof(Canvas));
        _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var canvas = _root.GetComponent<RectTransform>(); canvas.sizeDelta = new Vector2(1440, canvasHeight);
        var hud = new GameObject("TopHud", typeof(RectTransform)).GetComponent<RectTransform>();
        hud.SetParent(canvas, false); hud.anchorMin = Vector2.up; hud.anchorMax = Vector2.one;
        hud.pivot = new Vector2(.5f, 1); hud.sizeDelta = new Vector2(0, 200);
        var panel = new GameObject("StationPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(hud, false); panel.anchorMin = Vector2.up; panel.anchorMax = Vector2.up;
        panel.pivot = Vector2.up; panel.anchoredPosition = new Vector2(20, -210); panel.sizeDelta = new Vector2(500, 160);
        return panel;
    }
    [Test] public void PanelHeight_IsNotLimitedByTwoHundredUnitTopHud()
    {
        var panel = CreatePanelUnderShortHud(2200);
        StationPanelStyle2A.FitHeightToCanvas(panel, 800);
        Assert.That(panel.rect.height, Is.EqualTo(800).Within(.01f));
    }
    [Test] public void PanelHeight_RespectsCanvasBottomDespiteShortHudParent()
    {
        var panel = CreatePanelUnderShortHud(600);
        StationPanelStyle2A.FitHeightToCanvas(panel, 800);
        Assert.That(panel.rect.height, Is.EqualTo(290).Within(.01f));
        var bottom = panel.TransformPoint(new Vector3(0, panel.rect.yMin, 0));
        float bottomInCanvas = _root.transform.InverseTransformPoint(bottom).y;
        Assert.That(bottomInCanvas, Is.EqualTo(-200).Within(.01f));
    }

    [Test] public void StationHeaderIcon_IsImmediatelyLeftOfTitle()
    {
        _root = new GameObject("StationPanel", typeof(RectTransform));
        var panel = _root.GetComponent<RectTransform>(); panel.sizeDelta = new Vector2(500, 260);
        var titleObject = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI)); titleObject.transform.SetParent(panel, false);
        var title = titleObject.GetComponent<TMP_Text>(); title.SetText("ЗОЛОТАЯ ПРИСТАНЬ");
        title.rectTransform.anchorMin = Vector2.up; title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.sizeDelta = new Vector2(0, 48);
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f));
        try
        {
            var icon = StationPanelStyle2A.HeaderIcon(panel);
            StationPanelStyle2A.LayoutStationHeader(panel, title, icon, sprite);
            Assert.That(icon.sprite, Is.SameAs(sprite)); Assert.That(icon.enabled, Is.True); Assert.That(icon.preserveAspect, Is.True);
            var iconCorners = new Vector3[4]; var titleCorners = new Vector3[4];
            icon.rectTransform.GetWorldCorners(iconCorners); title.rectTransform.GetWorldCorners(titleCorners);
            Assert.That(titleCorners[0].x - iconCorners[2].x, Is.EqualTo(8).Within(.01));
        }
        finally { Object.DestroyImmediate(sprite); }
    }

    private Button ProgressFooter(out StationRuntimeState station, double duration, double elapsed)
    {
        _root = new GameObject("StationPanel", typeof(RectTransform));
        var rect = _root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(500, 260);
        var button = StationPanelStyle2A.ConstructionFooter(rect, null, Color.black, () => { }); button.gameObject.SetActive(true);
        station = new StationRuntimeState { Level = 0, StationType = StationType.Science,
            Development = new StationOperationRuntimeState { Kind = StationOperationKind.Construction, DurationSeconds = duration, ElapsedSeconds = elapsed } };
        return button;
    }
    [TestCase(60d, 0d, 0f, "01:00")]
    [TestCase(60d, 15d, .25f, "00:45")]
    [TestCase(60d, -10d, 0f, "01:00")]
    [TestCase(60d, 90d, 1f, "00:00")]
    [TestCase(0d, 0d, 1f, "00:00")]
    [TestCase(3661d, 0d, 0f, "01:01:01")]
    public void ConstructionFooter_ShowsRemainingTimeAndLeftToRightProgress(double duration, double elapsed, float progress, string remaining)
    {
        var button = ProgressFooter(out var station, duration, elapsed);
        StationPanelStyle2A.UpdateConstructionFooter(button, station);
        Assert.That(button.interactable, Is.False);
        Assert.That(button.GetComponent<Image>().color, Is.EqualTo((Color)new Color32(65, 73, 90, 255)));
        Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(remaining));
        var fill = button.transform.Find("ConstructionProgressFill").GetComponent<RectTransform>();
        Assert.That(fill.anchorMin, Is.EqualTo(Vector2.zero)); Assert.That(fill.anchorMax.x, Is.EqualTo(progress).Within(.001));
        Assert.That(fill.rect.width, Is.EqualTo(button.GetComponent<RectTransform>().rect.width * progress).Within(.01));
        Assert.That(fill.GetComponent<Image>().color, Is.EqualTo((Color)new Color32(38, 154, 79, 255)));
        Assert.That(fill.GetComponent<Image>().raycastTarget, Is.False);
    }
    [Test] public void PausedConstruction_DoesNotAdvanceDisplayWithoutSimulationProgress()
    {
        var button = ProgressFooter(out var station, 60, 15); station.Development.PauseReason = StationOperationPauseReason.UnderAttack;
        StationPanelStyle2A.UpdateConstructionFooter(button, station);
        StationPanelStyle2A.UpdateConstructionFooter(button, station);
        Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("00:45"));
        Assert.That(button.transform.Find("ConstructionProgressFill").GetComponent<RectTransform>().anchorMax.x, Is.EqualTo(.25f));
        Assert.That(button.transform.Find("ConstructionProgressFill").GetComponent<Image>().color, Is.EqualTo((Color)new Color32(180, 70, 70, 255)));
        Assert.That(station.Development.ElapsedSeconds, Is.EqualTo(15));
    }
    [Test] public void ReturningToUnbuiltState_RestoresBuildButtonAndHidesProgress()
    {
        var button = ProgressFooter(out var station, 60, 15); StationPanelStyle2A.UpdateConstructionFooter(button, station);
        station.Development = null; StationPanelStyle2A.UpdateConstructionFooter(button, station);
        Assert.That(button.interactable, Is.True); Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Построить"));
        Assert.That(button.transform.Find("ConstructionProgressFill").gameObject.activeSelf, Is.False);
    }
    [Test] public void ScienceBuildingCard_ShowsPowerAsThirdLine()
    {
        _root = new GameObject("PanelFixture"); var panel = _root.AddComponent<SystemStationPanel2A>();
        var card = new StationCardSnapshot { Station = new StationRuntimeState { Level = 0, StationType = StationType.Science,
            Development = new StationOperationRuntimeState { Kind = StationOperationKind.Construction, DurationSeconds = 60, ElapsedSeconds = 15 } } };
        var describe = typeof(SystemStationPanel2A).GetMethod("Describe", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(describe.Invoke(panel, new object[] { card }), Is.EqualTo("Тип станции: Научная станция\nСостояние: строительство\nСила исследований: 0 / 0"));
    }

    private sealed class ArrivalSession : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class ArrivalCards : IStationCardService
    {
        public int Cancels;
        public StationCardSnapshot GetCard(string id) => null;
        public System.Collections.Generic.IReadOnlyList<StationActionOffer> GetOffers(string id) => System.Array.Empty<StationActionOffer>();
        public bool Prepare(string systemId, StationOperationKind kind, string optionId, out StationActionConfirmation confirmation, out string reason)
        { confirmation = null; reason = string.Empty; return false; }
        public bool Confirm(string id, out string reason) { reason = string.Empty; return false; }
        public void CancelConfirmation() { Cancels++; }
    }
    private static void Field(object target, string name, object value) => target.GetType()
        .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    [Test] public void ArrivalRequest_KeepsVisibleStationCardAndPendingConfirmation()
    {
        _root = new GameObject("StationPanelHost"); var controller = _root.AddComponent<SystemStationPanel2A>();
        var panel = new GameObject("VisibleCard"); panel.transform.SetParent(_root.transform, false);
        var station = ScriptableObject.CreateInstance<StationConfig>();
        typeof(BaseConfig).GetField("id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(station, "station");
        var events = new SimpleEventBus(); int closes = 0; events.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(evt => closes++);
        var session = new ArrivalSession(); session.State.Player.CurrentSystemId = "sys";
        var cards = new ArrivalCards(); var confirmation = new StationActionConfirmation { TicketId = "keep" };
        Field(controller, "_panel", panel); Field(controller, "_session", session); Field(controller, "_events", events);
        Field(controller, "_cards", cards); Field(controller, "_initialized", true); Field(controller, "_systemId", "sys");
        Field(controller, "_stationId", "station"); Field(controller, "_confirmation", confirmation); Field(controller, "_tab", "services");
        try
        {
            typeof(SystemStationPanel2A).GetMethod("OnRequested", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, new object[] { new StationCardRequestedEvent(station) });
            Assert.That(panel.activeSelf, Is.True); Assert.That(closes, Is.Zero); Assert.That(cards.Cancels, Is.Zero);
            Assert.That(typeof(SystemStationPanel2A).GetField("_confirmation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(controller), Is.SameAs(confirmation));
            Assert.That(typeof(SystemStationPanel2A).GetField("_tab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(controller), Is.EqualTo("services"));
        }
        finally
        {
            Field(controller, "_panel", null); Object.DestroyImmediate(station);
        }
    }
    private sealed class ArrivalTarget : ITargetService2A
    {
        public TargetingRuntimeState State { get; } = new TargetingRuntimeState();
        public bool HasTarget { get; private set; } = true;
        public int Clears;
        public bool TrySelectTarget(string id, SystemGameplayTargetType type, Vector2 position, bool interactable, bool available, bool byPlayer, out TargetSelectionFailReason2A reason)
        { reason = default; HasTarget = true; return true; }
        public bool TryRefreshCurrentTarget(string id, Vector2 position, bool interactable, bool available, out TargetSelectionFailReason2A reason)
        { reason = default; return true; }
        public bool IsCurrentTarget(string id) => HasTarget;
        public void ClearTarget() { HasTarget = false; Clears++; }
    }
    [TestCase(TravelDestinationType.Station, 0, false)]
    [TestCase(TravelDestinationType.Station, 1, true)]
    [TestCase(TravelDestinationType.Station, 10, true)]
    [TestCase(TravelDestinationType.Planet, 0, true)]
    [TestCase(TravelDestinationType.MapPoint, 0, true)]
    public void StationArrival_PreservesSelectionOnlyForLevelZero(TravelDestinationType destination, int level, bool clear)
    {
        _root = new GameObject("DestinationController"); var controller = _root.AddComponent<SystemMapDestinationController2>();
        var target = new ArrivalTarget(); Field(controller, "_targetService", target);
        var session = new ArrivalSession(); session.State.Player.CurrentSystemId = "sys";
        session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "sys", Station = new StationRuntimeState { Level = level } });
        Field(controller, "_gameSessionService", session);
        typeof(SystemMapDestinationController2).GetMethod("OnTravelCompleted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(controller, new object[] { new SystemTravelCompletedEvent(destination, Vector3.zero, "", "", null, null) });
        Assert.That(target.Clears, Is.EqualTo(clear ? 1 : 0)); Assert.That(target.HasTarget, Is.EqualTo(!clear));
    }

    [TestCase(StationOperationKind.Construction, 0)] [TestCase(StationOperationKind.Upgrade, 2)]
    public void ObjectsList_StationNameIncludesLiveRemainingTime_AndRemovesItAfterCompletion(StationOperationKind kind, int level)
    {
        _root = new GameObject("ObjectsListFixture"); var hud = _root.AddComponent<SystemObjectsListHud2A>();
        var stationConfig = ScriptableObject.CreateInstance<StationConfig>(); var systemConfig = ScriptableObject.CreateInstance<StarSystemConfig>();
        typeof(BaseConfig).GetField("displayName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(stationConfig, "Золотая пристань");
        typeof(BaseConfig).GetField("id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(systemConfig, "sys");
        Field(systemConfig, "station", stationConfig);
        var session = new ArrivalSession(); session.State.Player.CurrentSystemId = "sys";
        var station = new StationRuntimeState { Level = level, Development = new StationOperationRuntimeState {
            Kind = kind, DurationSeconds = 60, ElapsedSeconds = 15 } };
        session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "sys", Station = station });
        Field(hud, "_gameSessionService", session);
        var append = typeof(SystemObjectsListHud2A).GetMethod("AppendStation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var builder = (System.Text.StringBuilder)typeof(SystemObjectsListHud2A).GetField("_builder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(hud);
        try
        {
            append.Invoke(hud, new object[] { systemConfig }); Assert.That(builder.ToString(), Does.Contain("Золотая пристань (00:45)"));
            Assert.That(builder.ToString(), Does.Contain((level == 0 ? "<color=#AAB7C4>" : "<color=#FFFFFF>") + "Золотая пристань (00:45)"));
            station.Development.ElapsedSeconds = 45; builder.Clear(); append.Invoke(hud, new object[] { systemConfig });
            Assert.That(builder.ToString(), Does.Contain("Золотая пристань (00:15)"));
            station.Development.PauseReason = StationOperationPauseReason.UnderAttack; builder.Clear(); append.Invoke(hud, new object[] { systemConfig });
            Assert.That(builder.ToString(), Does.Contain("Золотая пристань (00:15)"));
            station.Level = level + 1; station.Development = null; builder.Clear(); append.Invoke(hud, new object[] { systemConfig });
            Assert.That(builder.ToString(), Does.Not.Contain("(00:"));
            Assert.That(builder.ToString(), Does.Contain("<color=#FFFFFF>Золотая пристань"));
        }
        finally { Object.DestroyImmediate(stationConfig); Object.DestroyImmediate(systemConfig); }
    }

    [TestCase(1, 0, "нет")] [TestCase(5, 37, "37%")] [TestCase(10, 0, "нет")]
    public void BuiltIdleCard_ContainsExactlyTypeLevelHealthAndDamage(int level, int damage, string text)
    {
        _root = new GameObject("StationCardFixture"); var panel = _root.AddComponent<SystemStationPanel2A>();
        var maximum = StationInfrastructureRules.DefaultMaxHealth(level);
        var health = maximum - (int)System.Math.Ceiling(maximum * damage / 100d);
        var card = new StationCardSnapshot { Damage = damage, Station = new StationRuntimeState { Level = level, StationType = StationType.Science, HealthVersion = 1, HealthLevel = level, MaxHealth = maximum, Health = health } };
        var describe = typeof(SystemStationPanel2A).GetMethod("Describe", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(describe.Invoke(panel, new object[] { card }), Is.EqualTo("Тип станции: Научная станция\nСостояние: Уровень станции: " + level + "/10\nЗдоровье: " + health + " / " + maximum + "\nСила исследований: 0 / 0\nПовреждения: " + text));
    }
    [Test] public void UpgradeVisibility_UsesNextLevelGalaxyRequirement_AndHidesAtMaximum()
    {
        var config = ScriptableObject.CreateInstance<StationConfig>(); var effects = ScriptableObject.CreateInstance<StationLevelEffectsConfig>();
        effects.Levels = new[] { new StationLevelEffectsSettings { Level = 2, RequiredGalaxyLevel = 3 } }; Field(config, "levelEffects", effects);
        var card = new StationCardSnapshot { Config = config, GalaxyLevel = 2, Station = new StationRuntimeState { Level = 1 } };
        var show = typeof(SystemStationPanel2A).GetMethod("CanShowUpgrade", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        try
        {
            Assert.That(show.Invoke(null, new object[] { card }), Is.False);
            card.GalaxyLevel = 3; Assert.That(show.Invoke(null, new object[] { card }), Is.True);
            card.Station.Level = 10; card.GalaxyLevel = 10; Assert.That(show.Invoke(null, new object[] { card }), Is.False);
            card.Station.Level = 1; card.Station.Development = new StationOperationRuntimeState { Kind = StationOperationKind.Upgrade };
            Assert.That(show.Invoke(null, new object[] { card }), Is.False);
        }
        finally { Object.DestroyImmediate(config); Object.DestroyImmediate(effects); }
    }

    [TestCase(0, "нет")] [TestCase(25, "25%")]
    public void UpgradingCard_ShowsTargetLevelHealthAndDamage(int damage, string damageText)
    {
        _root = new GameObject("UpgradeCardFixture"); var panel = _root.AddComponent<SystemStationPanel2A>();
        int health = 450 - (int)System.Math.Ceiling(450 * damage / 100d);
        var card = new StationCardSnapshot { Damage = damage, Station = new StationRuntimeState { Level = 2, StationType = StationType.Science,
            HealthVersion = 1, HealthLevel = 2, Health = health, MaxHealth = 450,
            Development = new StationOperationRuntimeState { Kind = StationOperationKind.Upgrade, TargetLevel = 3, DurationSeconds = 60, ElapsedSeconds = 15 } } };
        var describe = typeof(SystemStationPanel2A).GetMethod("Describe", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(describe.Invoke(panel, new object[] { card }), Is.EqualTo("Тип станции: Научная станция\nСостояние: улучшение до уровня 3\nЗдоровье: " + health + " / 450\nСила исследований: 0 / 0\nПовреждения: " + damageText));
    }
    [Test] public void UpgradeFooter_IsRedUnderAttackAndGreenAfterResuming()
    {
        var button = ProgressFooter(out var station, 60, 15); station.Level = 2; station.Development.Kind = StationOperationKind.Upgrade;
        station.Development.PauseReason = StationOperationPauseReason.UnderAttack;
        StationPanelStyle2A.UpdateConstructionFooter(button, station); StationPanelStyle2A.UpdateConstructionFooter(button, station);
        Assert.That(button.interactable, Is.False); Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("00:45"));
        Assert.That(button.GetComponent<Image>().color, Is.EqualTo((Color)new Color32(65, 73, 90, 255)));
        var fill = button.transform.Find("ConstructionProgressFill").GetComponent<RectTransform>();
        Assert.That(fill.rect.width, Is.EqualTo(button.GetComponent<RectTransform>().rect.width * .25f).Within(.01));
        Assert.That(fill.GetComponent<Image>().color, Is.EqualTo((Color)new Color32(180, 70, 70, 255)));
        Assert.That(station.Development.ElapsedSeconds, Is.EqualTo(15));
        station.Development.PauseReason = StationOperationPauseReason.None;
        StationPanelStyle2A.UpdateConstructionFooter(button, station);
        Assert.That(fill.GetComponent<Image>().color, Is.EqualTo((Color)new Color32(38, 154, 79, 255)));
    }


    [TestCase(1)] [TestCase(5)] [TestCase(10)]
    public void DestroyedCard_HasHealthAsThirdLine_EvenWithPausedUpgrade(int level)
    {
        _root = new GameObject("DestroyedStationCard"); var panel = _root.AddComponent<SystemStationPanel2A>();
        var card = new StationCardSnapshot { Damage = 100, Distance = 2000, Station = new StationRuntimeState {
            Level = level, StationType = StationType.Military, IsDestroyed = true,
            HealthVersion = 1, HealthLevel = level, Health = 0, MaxHealth = StationInfrastructureRules.DefaultMaxHealth(level),
            Development = new StationOperationRuntimeState { Kind = StationOperationKind.Upgrade, TargetLevel = level + 1 } } };
        var describe = typeof(SystemStationPanel2A).GetMethod("Describe",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(describe.Invoke(panel, new object[] { card }), Is.EqualTo(
            "Тип станции: Военная база\nСостояние: Уровень станции " + level + "/10\nЗдоровье: 0 / " + StationInfrastructureRules.DefaultMaxHealth(level) + "\nПопуляция военных: 0 / 0\nПовреждение: разрушена"));
    }
}
