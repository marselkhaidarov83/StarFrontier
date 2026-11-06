using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ResearchTreeAndServiceEditModeTests
{
    private ResearchTreeConfig _tree;
    private ResearchBranchConfig _branch;
    private ResearchNode _node;
    [SetUp] public void Setup()
    {
        _tree = ScriptableObject.CreateInstance<ResearchTreeConfig>();
        _branch = ScriptableObject.CreateInstance<ResearchBranchConfig>();
        _node = Node(ResearchUpgrade.MilitarySuperiority);
        _branch.Nodes = new[] { _node }; _tree.Branches = new[] { _branch };
    }
    [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(_tree); UnityEngine.Object.DestroyImmediate(_branch); }
    private static ResearchNode Node(ResearchUpgrade upgrade) => new ResearchNode
    {
        Upgrade = upgrade, Levels = Enumerable.Range(1, 10).Select(i => new ResearchNodeLevel {
            Level = i, PaymentOptions = new[] { new StationPaymentOption { Id = "pay", DurationSeconds = 100,
                Costs = new[] { new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = i } } } } }).ToArray()
    };
    private static ResearchRuntimeState Active(int target = 1) => new ResearchRuntimeState
    {
        ActiveUpgrade = ResearchUpgrade.MilitarySuperiority,
        Active = new StationOperationRuntimeState { OperationId = "op", TargetLevel = target, DurationSeconds = 100 }
    };
    [Test] public void Tree_WithTenLevelsIsValid() => Assert.That(ResearchRules.Errors(_tree), Is.Empty);
    [Test] public void MissingLevel_IsRejected()
    { _node.Levels = _node.Levels.Take(9).ToArray(); Assert.That(ResearchRules.Errors(_tree), Does.Contain("TenLevelsRequired")); }
    [Test] public void DuplicateUpgrade_IsRejected()
    { _branch.Nodes = new[] { _node, Node(_node.Upgrade) }; Assert.That(ResearchRules.Errors(_tree), Does.Contain("DuplicateUpgrade")); }
    [Test] public void CrossBranchDependency_IsRejected()
    { _node.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = ResearchUpgrade.TradeSuperiority } };
        Assert.That(ResearchRules.Errors(_tree), Does.Contain("DependencyOutsideBranch")); }
    [Test] public void CyclicDependency_IsRejected()
    { _node.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = _node.Upgrade } };
        Assert.That(ResearchRules.Errors(_tree), Does.Contain("ResearchCycle")); }
    [Test] public void Dependencies_RequireConfiguredReachedLevel()
    {
        var next = Node(ResearchUpgrade.TradeSuperiority);
        next.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = _node.Upgrade, RequiredLevel = 3 } };
        _branch.Nodes = new[] { _node, next }; var state = new ResearchRuntimeState();
        state.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 2 });
        Assert.That(ResearchRules.TryQuote(_tree, state, next.Upgrade, "pay", out _, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("DependenciesNotMet"));
        state.Levels[0].Level = 3;
        Assert.That(ResearchRules.TryQuote(_tree, state, next.Upgrade, "pay", out _, out _), Is.True);
    }
    [Test] public void LevelTen_CannotBeResearchedAgain()
    {
        var state = new ResearchRuntimeState(); state.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 10 });
        Assert.That(ResearchRules.TryQuote(_tree, state, _node.Upgrade, "pay", out _, out _), Is.False);
    }
    [Test] public void AnotherActiveResearch_IsRejected()
    { Assert.That(ResearchRules.TryQuote(_tree, Active(), _node.Upgrade, "pay", out _, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("ResearchAlreadyRunning")); }
    [Test] public void RateZero_PreservesPaidProgress()
    {
        var state = Active(); state.Active.ElapsedSeconds = 25;
        string before = JsonUtility.ToJson(state);
        Assert.That(ResearchRules.Advance(state, 10000, 0, out _, out _), Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
    }
    [Test] public void ScienceRate_ScalesElapsedWork()
    {
        var state = Active(); ResearchRules.Advance(state, 10, 3, out _, out _);
        Assert.That(state.Active.ElapsedSeconds, Is.EqualTo(30));
    }
    [Test] public void Completion_ChangesEnumLevelExactlyOnce()
    {
        var state = Active();
        Assert.That(ResearchRules.Advance(state, 50, 2, out var upgrade, out int level), Is.True);
        Assert.That(upgrade, Is.EqualTo(_node.Upgrade)); Assert.That(level, Is.EqualTo(1));
        Assert.That(state.GetLevel(_node.Upgrade), Is.EqualTo(1));
        Assert.That(ResearchRules.Advance(state, 1000, 2, out _, out _), Is.False);
        Assert.That(state.Levels.Count, Is.EqualTo(1));
    }
    [Test] public void SecondLevel_UpdatesExistingEnumEntry()
    {
        var state = Active(2); state.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 1 });
        ResearchRules.Advance(state, 100, 1, out _, out _);
        Assert.That(state.Levels.Count, Is.EqualTo(1)); Assert.That(state.GetLevel(_node.Upgrade), Is.EqualTo(2));
    }
    [Test] public void SaveRoundTrip_KeepsCostsProgressAndLevels()
    {
        var state = Active(); state.Active.ElapsedSeconds = 75;
        state.Active.PaidCosts = new[] { new StationPaymentCostSnapshot { Resource = StationPaymentResource.Diamonds, Amount = 5 } };
        var galaxy = new GalaxyRuntimeState { Research = state };
        galaxy = JsonUtility.FromJson<GalaxyRuntimeState>(JsonUtility.ToJson(galaxy));
        Assert.That(galaxy.Research.Active.PaidCosts[0].Amount, Is.EqualTo(5));
        Assert.That(galaxy.Research.Active.ElapsedSeconds, Is.EqualTo(75));
        Assert.That(ResearchRules.Advance(galaxy.Research, 25, 1, out _, out _), Is.True);
    }
    [Test] public void NullPlaceholder_NormalizesWithoutRemovingRealOperation()
    {
        var state = new ResearchRuntimeState { Active = new StationOperationRuntimeState() }; state.Normalize();
        Assert.That(state.Active, Is.Null); state.Active = Active().Active; state.Normalize(); Assert.That(state.Active, Is.Not.Null);
    }
    [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)]
    public void CorruptClock_IsRejected(double value)
    { var state = Active(); state.Active.ElapsedSeconds = value; Assert.That(ResearchRules.StateErrors(state), Is.Not.Empty); }
    [Test] public void DuplicateSavedEnum_IsRejected()
    { var state = new ResearchRuntimeState(); state.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 1 });
        state.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 2 }); Assert.That(ResearchRules.StateErrors(state), Does.Contain("DuplicateResearchLevel")); }

    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private sealed class Infrastructure : IStationInfrastructureService
    {
        public double Rate = 1;
        public void InitializeSession() { }
        public float GetRangerAttackProbability(string systemId, out bool configured) { configured = false; return 0; }
        public double GetScienceRate() => Rate;
        public bool CanTravel(SystemNpcRuntimeState npc, string targetSystemId) => true;
        public void AdvanceTime(double seconds) { }
        public double NextRepairBoundary(double maximumSeconds) => maximumSeconds;
        public void CompleteTick(int tick) { }
        public void AdvanceOfflineRepair(double seconds) { }
        public void RelocateStationMilitary() { }
    }
    private ResearchService Service(Session session, SimpleEventBus events, Infrastructure infrastructure)
    {
        session.State.Player.CurrentSystemId = "science"; session.State.Player.CurrentStationId = "station"; session.State.Player.Diamonds = 10;
        session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "science", Station = new StationRuntimeState {
            ConfigId = "station", StationType = StationType.Science, Level = 1 } });
        return new ResearchService(_tree, session, null, infrastructure, new StationResourcePaymentGateway(session), events, _ => true);
    }
    [Test] public void Service_DuplicateStartSpendsOnce()
    {
        var session = new Session(); var service = Service(session, new SimpleEventBus(), new Infrastructure());
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.True);
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.False);
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(9));
    }
    [Test] public void Service_FailedPaymentKeepsIdleResearch()
    {
        var session = new Session(); var service = Service(session, new SimpleEventBus(), new Infrastructure()); session.State.Player.Diamonds = 0;
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.False);
        Assert.That(session.State.Galaxy.Research.Active, Is.Null);
    }
    [Test] public void Service_ForeignStationCannotStartResearch()
    {
        var session = new Session(); var service = Service(session, new SimpleEventBus(), new Infrastructure());
        Assert.That(service.TryStart("other", _node.Upgrade, "pay", 0, out _), Is.False);
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(10));
    }
    [Test] public void Service_SnapshotMutationDoesNotModifyLiveResearch()
    {
        var session = new Session(); var service = Service(session, new SimpleEventBus(), new Infrastructure());
        service.TryStart("science", _node.Upgrade, "pay", 0, out _);
        var snapshot = service.GetSnapshot(); snapshot.Active.ElapsedSeconds = 99;
        Assert.That(session.State.Galaxy.Research.Active.ElapsedSeconds, Is.Zero);
    }
    [Test] public void Service_CompletionObserverFailureCannotDuplicateOrBlockAchievement()
    {
        var session = new Session(); var events = new SimpleEventBus(); var service = Service(session, events, new Infrastructure()); int delivered = 0;
        events.Subscribe<ResearchCompletedEvent>(_ => throw new InvalidOperationException("observer"));
        events.Subscribe<ResearchCompletedEvent>(_ => delivered++);
        service.TryStart("science", _node.Upgrade, "pay", 0, out _); service.AdvanceTime(100); service.AdvanceTime(100);
        Assert.That(delivered, Is.EqualTo(1)); Assert.That(service.GetSnapshot().GetLevel(_node.Upgrade), Is.EqualTo(1));
    }
    [Test] public void Service_AllScienceDestroyedPausesThenResumesWithoutRepayment()
    {
        var session = new Session(); var infrastructure = new Infrastructure(); var service = Service(session, new SimpleEventBus(), infrastructure);
        service.TryStart("science", _node.Upgrade, "pay", 0, out _); service.AdvanceTime(25);
        infrastructure.Rate = 0; service.AdvanceTime(1000);
        Assert.That(service.GetSnapshot().Active.ElapsedSeconds, Is.EqualTo(25));
        infrastructure.Rate = 3; service.AdvanceTime(25);
        Assert.That(service.GetSnapshot().GetLevel(_node.Upgrade), Is.EqualTo(1)); Assert.That(session.State.Player.Diamonds, Is.EqualTo(9));
    }
    [Test] public void IdleSnapshot_DoesNotExposeUnityNullPlaceholder()
    {
        var session = new Session(); var service = Service(session, new SimpleEventBus(), new Infrastructure());
        Assert.That(service.GetSnapshot().Active, Is.Null);
    }
    [Test] public void UnifiedOfflineWindow_IsCappedAndDoesNotReplayResearch()
    {
        var session = new Session(); var events = new SimpleEventBus(); var infrastructure = new Infrastructure();
        var research = Service(session, events, infrastructure); _node.Levels[0].PaymentOptions[0].DurationSeconds = 100000;
        Assert.That(research.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.True);
        var stations = new StationService(session, _ => null, events, new StationResourcePaymentGateway(session), infrastructure, research);
        long from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        session.State.Meta.LastSaveUtc = from;
        stations.ProcessOffline(session.State, from + TimeSpan.FromHours(24).Ticks);
        Assert.That(research.GetSnapshot().Active.ElapsedSeconds, Is.EqualTo(12 * 3600));
        stations.ProcessOffline(session.State, from + TimeSpan.FromHours(24).Ticks);
        Assert.That(research.GetSnapshot().Active.ElapsedSeconds, Is.EqualTo(12 * 3600));
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(9));
    }


    [Test] public void FirstLevelGalaxyGate_HidesAllTransitiveDependentsButKeepsIndependentNodes()
    {
        var middle = Node(ResearchUpgrade.TradeSuperiority);
        var tail = Node(ResearchUpgrade.MilitaryDefence);
        var independent = Node(ResearchUpgrade.TradeSupply);
        _node.RequiredGalaxyLevel = 3;
        middle.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = _node.Upgrade, RequiredLevel = 10 } };
        tail.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = middle.Upgrade, RequiredLevel = 10 } };
        _branch.Nodes = new[] { _node, middle, tail, independent };
        Assert.That(ResearchRules.VisibleNodes(_branch, 2).Select(n => n.Upgrade), Is.EqualTo(new[] { independent.Upgrade }));
        Assert.That(ResearchRules.VisibleNodes(_branch, 3).Count(), Is.EqualTo(4));
    }
    [Test] public void FirstLevelGateOnLevelAsset_AlsoHidesDependents()
    {
        _node.Levels[0].RequiredGalaxyLevel = 4;
        var next = Node(ResearchUpgrade.TradeSuperiority);
        next.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = _node.Upgrade } };
        _branch.Nodes = new[] { _node, next };
        Assert.That(ResearchRules.VisibleNodes(_branch, 3), Is.Empty);
    }
    [Test] public void NextLevelGalaxyGate_KeepsNodeVisibleButRejectsQuote()
    {
        _node.Levels[1].RequiredGalaxyLevel = 3;
        var state = new ResearchRuntimeState(); state.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 1 });
        Assert.That(ResearchRules.VisibleNodes(_branch, 1).Single(), Is.SameAs(_node));
        Assert.That(ResearchRules.TryQuote(_tree, state, _node.Upgrade, "pay", out _, out string reason, 1), Is.False);
        Assert.That(reason, Is.EqualTo("GalaxyLevelRequired"));
        Assert.That(ResearchRules.TryQuote(_tree, state, _node.Upgrade, "pay", out _, out _, 3), Is.True);
    }
    [Test] public void Service_GalaxyGateIsCheckedBeforeResourcePayment()
    {
        _node.RequiredGalaxyLevel = 2;
        var session = new Session(); var service = Service(session, new SimpleEventBus(), new Infrastructure());
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("GalaxyLevelRequired")); Assert.That(session.State.Player.Diamonds, Is.EqualTo(10));
        Assert.That(session.State.Galaxy.Research.Active, Is.Null);
        session.State.Galaxy.Sectors.Add(new SectorRuntimeState { IsUnlocked = true });
        session.State.Galaxy.Sectors.Add(new SectorRuntimeState { IsUnlocked = true });
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.True);
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(9));
    }
    [Test] public void HiddenAncestorCannotBeBypassedBySavedDependencyLevels()
    {
        _node.RequiredGalaxyLevel = 4;
        var next = Node(ResearchUpgrade.TradeSuperiority);
        next.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = _node.Upgrade, RequiredLevel = 10 } };
        _branch.Nodes = new[] { _node, next };
        var state = new ResearchRuntimeState(); state.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 10 });
        Assert.That(ResearchRules.TryQuote(_tree, state, next.Upgrade, "pay", out _, out string reason, 2), Is.False);
        Assert.That(reason, Is.EqualTo("GalaxyLevelRequired"));
    }
    [Test] public void ProductionResearchTree_HasFiveBranchesTenLevelsAndValidLevelDependencies()
    {
        var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchTreeConfig>(
            "Assets/_Project/Content/Configs/Stations2A/Research/research_tree_stage2a.asset");
        Assert.That(tree, Is.Not.Null); Assert.That(ResearchRules.Errors(tree), Is.Empty);
        Assert.That(tree.Branches.Length, Is.EqualTo(5));
        foreach (var branch in tree.Branches)
        {
            Assert.That(branch.Nodes.Length, Is.GreaterThanOrEqualTo(3));
            for (int i = 0; i < branch.Nodes.Length; i++)
            {
                Assert.That(branch.Nodes[i].Levels.Length, Is.EqualTo(10));
                foreach (var level in branch.Nodes[i].Levels)
                {
                    Assert.That(level, Is.Not.Null);
                    foreach (var dependency in level.Dependencies ?? Array.Empty<ResearchDependency>())
                    {
                        Assert.That(dependency, Is.Not.Null);
                        Assert.That(dependency.RequiredLevel, Is.InRange(1, 10));
                        Assert.That(branch.Nodes.Any(n => n != null && n.Upgrade == dependency.Upgrade &&
                            n.Levels.Any(l => l != null && l.Level == dependency.RequiredLevel)), Is.True,
                            "Dependency must reference an existing level inside its branch");
                    }
                }
            }
        }
    }
    [Test] public void SharedResearchCardPrefab_ReusesRendererAcrossLockedRunningAndCompleteStates()
    {
        var prefab = Resources.Load<GameObject>("UI/ResearchCard2A"); Assert.That(prefab, Is.Not.Null);
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var card = instance.GetComponent<ResearchCardView2A>(); Assert.That(card, Is.Not.Null);
            card.Bind(new ResearchCardData2A { Title = "Заблокировано", Level = 1, Locked = true,
                Condition = "Следующий уровень требует уровня галактики 3" }, null, null);
            Assert.That(card.PaymentButtonCount, Is.Zero);
            card.Bind(new ResearchCardData2A { Title = "Исследуется", Running = true, Progress = .5f, ProgressText = "Осталось 01:00" }, null, null);
            Assert.That(instance.transform.Find("ResearchProgress").gameObject.activeSelf, Is.True);
            card.Bind(new ResearchCardData2A { Title = "Завершено", Level = 10, Completed = true }, null, null);
            Assert.That(instance.transform.Find("ResearchProgress").gameObject.activeSelf, Is.False);
            Assert.That(instance.GetComponents<ResearchCardView2A>().Length, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    [Test] public void VisibleCard_NextGalaxyLevelLocked_HasNoResearchButtons()
    {
        _node.Title = "Первое исследование"; _node.Levels[1].RequiredGalaxyLevel = 3;
        var snapshot = new ResearchRuntimeState(); snapshot.Levels.Add(new ResearchUpgradeLevel { Upgrade = _node.Upgrade, Level = 1 });
        var root = new GameObject("ResearchDescribeFixture"); root.SetActive(false);
        try
        {
            var controller = root.AddComponent<StationResearchPanelController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(StationResearchPanelController).GetField("_snapshot", flags).SetValue(controller, snapshot);
            var describe = typeof(StationResearchPanelController).GetMethod("Describe", flags);
            var data = (ResearchCardData2A)describe.Invoke(controller, new object[] { _node, 1, _branch });
            Assert.That(data.Title, Is.EqualTo("Первое исследование")); Assert.That(data.Level, Is.EqualTo(1));
            Assert.That(data.Choices, Is.Empty); Assert.That(data.Condition, Does.Not.Contain("галактики")); Assert.That(data.Locked, Is.True);
            Assert.That(ResearchRules.VisibleNodes(_branch, 1).Single(), Is.SameAs(_node));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void AtomGraphic_CreatesCanvasRendererAndCanBeEnabledInsideClippedList()
    {
        var root = new GameObject("ResearchClipFixture", typeof(RectTransform), typeof(Canvas));
        try
        {
            var clip = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            clip.transform.SetParent(root.transform, false);
            var iconObject = new GameObject("AtomIcon", typeof(RectTransform)); iconObject.transform.SetParent(clip.transform, false);
            var icon = iconObject.AddComponent<ResearchButtonIcon2A>();
            Assert.That(iconObject.GetComponent<CanvasRenderer>(), Is.Not.Null);
            Assert.That(icon.canvasRenderer, Is.SameAs(iconObject.GetComponent<CanvasRenderer>()));
            Assert.DoesNotThrow(() => {
                Canvas.ForceUpdateCanvases(); iconObject.SetActive(false); iconObject.SetActive(true);
                Canvas.ForceUpdateCanvases(); root.SetActive(false);
            });
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void ResearchScreen_IsInsideBackgroundAboveItsImageAndBelowLocationActions()
    {
        var root = new GameObject("PlanetRoot", typeof(RectTransform));
        try
        {
            var background = new GameObject("BackgroundImage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(root.transform, false);
            var government = new GameObject("GovernmentBlock", typeof(RectTransform)); government.transform.SetParent(background.transform, false);
            var action = new GameObject("Takeoff", typeof(RectTransform)); action.transform.SetParent(background.transform, false);
            var screen = new GameObject("StationResearchBlock", typeof(RectTransform)); screen.transform.SetParent(root.transform, false);
            StationResearchPanelView2A.AttachToLocation(screen.GetComponent<RectTransform>(), root.transform, government.transform, background.transform);
            Assert.That(screen.transform.parent, Is.SameAs(background.transform));
            Assert.That(screen.transform.parent, Is.SameAs(government.transform.parent));
            Assert.That(screen.transform.GetSiblingIndex(), Is.LessThan(action.transform.GetSiblingIndex()));
            var view = screen.AddComponent<StationResearchPanelView2A>(); view.Build(null);
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<StationConfig>(
                "Assets/_Project/Content/Configs/Galaxy/Stations/station_s01_science_vega_reach_01.asset");
            Assert.That(config.StationTypeConfig.MissionIllustration, Is.Not.Null);
            view.SetStation(new StationCardSnapshot { Config = config, GalaxyLevel = 5, Station = new StationRuntimeState {
                Level = 3, HealthVersion = 1, HealthLevel = 3, Health = 500, MaxHealth = 500 } }, config.StationTypeConfig.MissionIllustration);
            var hero = screen.transform.Find("LocationIllustration").GetComponent<UnityEngine.UI.Image>();
            Assert.That(hero.enabled, Is.True); Assert.That(hero.sprite, Is.SameAs(config.StationTypeConfig.MissionIllustration));
            Assert.That(screen.transform.Find("ResearchWorkspace").gameObject.activeInHierarchy, Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test] public void ResearchStationUpgrade_IsBottomRightAndUsesSharedProgressRenderer()
    {
        var root = new GameObject("ResearchUpgradeLayout", typeof(RectTransform));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2200);
            var view = root.AddComponent<StationResearchPanelView2A>(); view.Build(null);
            var station = new StationRuntimeState { Level = 1, HealthVersion = 1, HealthLevel = 1, Health = 400, MaxHealth = 400,
                Development = new StationOperationRuntimeState { Kind = StationOperationKind.Upgrade, TargetLevel = 2, DurationSeconds = 30, ElapsedSeconds = 15 } };
            view.SetStation(new StationCardSnapshot { Station = station, GalaxyLevel = 5 }, null);
            var button = view.Upgrade.GetComponent<RectTransform>(); var info = (RectTransform)button.parent;
            Assert.That(info.rect.width - button.anchoredPosition.x - button.rect.width, Is.EqualTo(18).Within(.01));
            Assert.That(info.rect.height + button.anchoredPosition.y - button.rect.height, Is.EqualTo(20).Within(.01));
            var fill = view.Upgrade.transform.Find("ConstructionProgressFill").GetComponent<RectTransform>();
            Assert.That(fill.anchorMax.x, Is.EqualTo(.5f).Within(.001));
            Assert.That(view.Upgrade.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("00:15"));
            Assert.That(view.Upgrade.interactable, Is.False);
            station.Development.PauseReason = StationOperationPauseReason.UnderAttack;
            view.SetStation(new StationCardSnapshot { Station = station, GalaxyLevel = 5 }, null);
            Assert.That(fill.anchorMax.x, Is.EqualTo(.5f).Within(.001));
            station.Development = null; view.SetStation(new StationCardSnapshot { Station = station, GalaxyLevel = 5 }, null);
            Assert.That(fill.gameObject.activeSelf, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test] public void ResearchGroupButtons_KeepOutlineInsideScrollMask()
    {
        var root = new GameObject("ResearchGroupLayout", typeof(RectTransform));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2200);
            var view = root.AddComponent<StationResearchPanelView2A>(); view.Build(null);
            view.SetGroups(new[] { _branch }, 0, _ => { });
            var viewport = root.transform.Find("ResearchWorkspace/ResearchGroups/Viewport").GetComponent<RectTransform>();
            var content = viewport.Find("Content").GetComponent<RectTransform>();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var button = content.GetChild(0).GetComponent<RectTransform>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, button);
            Assert.That(bounds.max.y + 2, Is.LessThanOrEqualTo(viewport.rect.yMax + .01f));
            Assert.That(bounds.min.y - 2, Is.GreaterThanOrEqualTo(viewport.rect.yMin - .01f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test] public void RangerResearchBranch_IsFifthWithTenLevelsAndLinearDependencies()
    {
        var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchTreeConfig>(
            "Assets/_Project/Content/Configs/Stations2A/Research/research_tree_stage2a.asset");
        var ranger = tree.Branches[4];
        Assert.That(ranger.DisplayName, Is.EqualTo("Рейнджерское превосходство"));
        Assert.That(ranger.Nodes.Select(n => n.Upgrade), Is.EqualTo(new[] {
            ResearchUpgrade.RangerTraining, ResearchUpgrade.RangerPatrols, ResearchUpgrade.RangerMastery }));
        Assert.That(ResearchRules.VisibleNodes(ranger, 1), Is.Empty);
        Assert.That(ResearchRules.VisibleNodes(ranger, 2).Count(), Is.EqualTo(1));
        Assert.That(ResearchRules.Errors(tree), Is.Empty);
    }

    [Test] public void OtherStation_CanResearchOwnBranchButCannotPayForForeignBranch()
    {
        var session = new Session(); var service = Service(session, new SimpleEventBus(), new Infrastructure());
        session.State.Galaxy.Systems[0].Station.StationType = StationType.Trade;
        _branch.StationType = StationType.Military;
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("ResearchBranchUnavailable")); Assert.That(session.State.Player.Diamonds, Is.EqualTo(10));
        _branch.StationType = StationType.Trade;
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.True);
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(9));
    }
    [Test] public void StationDirections_AreExplicitlyBoundAcrossAllFiveAssets()
    {
        var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchTreeConfig>(
            "Assets/_Project/Content/Configs/Stations2A/Research/research_tree_stage2a.asset");
        Assert.That(tree.Branches.Select(b => b.StationType).Distinct().Count(), Is.EqualTo(5));
        Assert.That(ResearchRules.BranchForStation(tree, StationType.RangerBase).Id, Is.EqualTo("research_branch_05"));
        foreach (var branch in tree.Branches)
        {
            Assert.That(ResearchRules.CanAccessUpgrade(tree, branch.StationType, branch.Nodes[0].Upgrade), Is.True);
            Assert.That(ResearchRules.CanAccessUpgrade(tree, StationType.Science, branch.Nodes[0].Upgrade), Is.True);
        }
    }
    [Test] public void SingleDirectionScreen_HidesGroupSelectorAndResearchCardHasOneButton()
    {
        var root = new GameObject("StationDirectionFixture", typeof(RectTransform));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2200);
            var view = root.AddComponent<StationResearchPanelView2A>(); view.Build(null);
            view.SetGroups(new[] { _branch }, 0, _ => { }, false);
            Assert.That(root.transform.Find("ResearchWorkspace/ResearchGroups").gameObject.activeSelf, Is.False);
            view.SetHeading("Branch", 5, string.Empty);
            Assert.That(root.transform.Find("ResearchWorkspace/BranchTitle").gameObject.activeSelf, Is.False);
            var cardRoot = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("UI/ResearchCard2A"), root.transform);
            var card = cardRoot.GetComponent<ResearchCardView2A>();
            card.Bind(new ResearchCardData2A { Title = "Узел", Condition = string.Empty,
                Choices = new[] { new ResearchPaymentChoice2A { Label = "Исследовать", Available = true } } }, null, _ => { });
            Assert.That(cardRoot.GetComponentsInChildren<UnityEngine.UI.Button>().Length, Is.EqualTo(1));
            Assert.That(cardRoot.transform.Find("Condition").gameObject.activeSelf, Is.False);
            Assert.That(cardRoot.GetComponentInChildren<UnityEngine.UI.Button>().GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("Исследовать"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResearchTitle_LevelZeroShowsTargetOnlyWhileRunning(bool running)
    {
        var root = new GameObject("ResearchTitle", typeof(RectTransform));
        try
        {
            var view = root.AddComponent<ResearchCardView2A>();
            view.Bind(new ResearchCardData2A { Title = "Исследование", Level = 0, Running = running, TargetLevel = 1 }, null, null);
            Assert.That(root.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo(running ? "Исследование (уровень -> 1)" : "Исследование"));
            Assert.That(root.transform.Find("Level").gameObject.activeSelf, Is.False);
            view.Bind(new ResearchCardData2A { Title = "Исследование", Level = 1, Running = running, TargetLevel = 2 }, null, null);
            Assert.That(root.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo(running ? "Исследование (уровень 1 -> 2)" : "Исследование (уровень 1)"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase(10, 1, false)]
    [TestCase(40, 1, true)]
    [TestCase(40, 0, false)]
    public void PaidResearch_ReloadAndOfflinePreserveOperationOrCompleteWithoutPayingAgain(int offlineSeconds, int scienceRate, bool completes)
    {
        var session = new Session(); var events = new SimpleEventBus(); var infrastructure = new Infrastructure();
        var initial = Service(session, events, infrastructure);
        Assert.That(initial.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.True);
        initial.AdvanceTime(75);
        string operation = initial.GetSnapshot().Active.OperationId;
        long from = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc).Ticks;
        session.State.Meta.LastSaveUtc = from;
        session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(session.State)));
        infrastructure.Rate = scienceRate;
        var restored = new ResearchService(_tree, session, null, infrastructure,
            new StationResourcePaymentGateway(session), events, _ => true);
        restored.InitializeSession();
        var stations = new StationService(session, _ => null, events, new StationResourcePaymentGateway(session), infrastructure, restored);
        long now = from + TimeSpan.FromSeconds(offlineSeconds).Ticks;
        stations.ProcessOffline(session.State, now);
        var snapshot = restored.GetSnapshot();
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(9));
        Assert.That(snapshot.GetLevel(_node.Upgrade), Is.EqualTo(completes ? 1 : 0));
        if (completes) Assert.That(snapshot.Active, Is.Null);
        else
        {
            Assert.That(snapshot.Active.OperationId, Is.EqualTo(operation));
            Assert.That(snapshot.Active.ElapsedSeconds, Is.EqualTo(75 + offlineSeconds * scienceRate));
            Assert.That(restored.TryStart("science", _node.Upgrade, "pay", snapshot.Revision, out _), Is.False);
        }
        string applied = JsonUtility.ToJson(session.State.Galaxy.Research);
        stations.ProcessOffline(session.State, now);
        Assert.That(JsonUtility.ToJson(session.State.Galaxy.Research), Is.EqualTo(applied));
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(9));
    }

    [Test] public void PerLevelDependency_OnlyBlocksItsConfiguredLevelAndCannotSpend()
    {
        var prerequisite = Node(ResearchUpgrade.TradeSuperiority);
        _branch.Nodes = new[] { _node, prerequisite };
        _node.Levels[1].Dependencies = new[] { new ResearchDependency { Upgrade = prerequisite.Upgrade, RequiredLevel = 3 } };
        var session = new Session(); var events = new SimpleEventBus(); var infra = new Infrastructure();
        var service = Service(session, events, infra);
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", 0, out _), Is.True);
        service.AdvanceTime(100);
        int balance = session.State.Player.Diamonds;
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", service.GetSnapshot().Revision, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("DependenciesNotMet"));
        Assert.That(session.State.Player.Diamonds, Is.EqualTo(balance));
        session.State.Galaxy.Research.Levels.Add(new ResearchUpgradeLevel { Upgrade = prerequisite.Upgrade, Level = 3 });
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", service.GetSnapshot().Revision, out _), Is.True);
        service.AdvanceTime(100);
        Assert.That(service.TryStart("science", _node.Upgrade, "pay", service.GetSnapshot().Revision, out _), Is.True);
    }
    [Test] public void LevelGraph_AllowsReciprocalResearchNamesWhenLevelOrderIsAcyclic()
    {
        var other = Node(ResearchUpgrade.TradeSuperiority); _branch.Nodes = new[] { _node, other };
        other.Levels[0].Dependencies = new[] { new ResearchDependency { Upgrade = _node.Upgrade, RequiredLevel = 1 } };
        _node.Levels[1].Dependencies = new[] { new ResearchDependency { Upgrade = other.Upgrade, RequiredLevel = 1 } };
        Assert.That(ResearchRules.Errors(_tree), Is.Empty);
        other.Levels[0].Dependencies[0].RequiredLevel = 2;
        Assert.That(ResearchRules.Errors(_tree), Does.Contain("ResearchCycle"));
    }
    [Test] public void HigherLevelDependency_DoesNotHideTheFirstLevel()
    {
        var other = Node(ResearchUpgrade.TradeSuperiority); other.RequiredGalaxyLevel = 4;
        _branch.Nodes = new[] { _node, other };
        _node.Levels[1].Dependencies = new[] { new ResearchDependency { Upgrade = other.Upgrade, RequiredLevel = 2 } };
        Assert.That(ResearchRules.VisibleNodes(_branch, 1), Does.Contain(_node));
        Assert.That(ResearchRules.IsLevelVisible(_branch, _node, 2, 1), Is.False);
        Assert.That(ResearchRules.IsLevelVisible(_branch, _node, 2, 4), Is.True);
    }
}
