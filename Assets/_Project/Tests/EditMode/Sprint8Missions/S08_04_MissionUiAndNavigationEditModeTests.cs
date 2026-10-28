using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Public and unsealed so DispatchProxy can generate a test implementation.
public class S08_04_ConfigProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler;
    protected override object Invoke(MethodInfo method, object[] args) => Handler(method, args);

    public static IConfigService Make(Func<MethodInfo, object[], object> handler)
    {
        var config = Create<IConfigService, S08_04_ConfigProxy>();
        ((S08_04_ConfigProxy)(object)config).Handler = handler;
        return config;
    }
}

public sealed class S08_04_MissionUiAndNavigationEditModeTests
{
    private readonly List<UnityEngine.Object> _assets = new();

    [TearDown]
    public void TearDown()
    {
        foreach (UnityEngine.Object asset in _assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void DeliveryCardNamesCargoSpacePlanetAndSystem()
    {
        var mission = new MissionInstanceData
        {
            MissionType = MissionType.Delivery,
            Status = MissionStatus.InProgress,
            TargetPlanetId = "planet_iris",
            TargetSystemId = "system_delta",
            Objective = new MissionObjectiveData
            {
                MissionCargoItemId = "medicine",
                MissionCargoAmount = 4,
                MissionCargoVolume = 12
            }
        };
        string description = MissionUiText.Description(mission, null);
        StringAssert.Contains("ценный груз", description);
        StringAssert.Contains("12", description);
        StringAssert.Contains("planet_iris", description);
        StringAssert.Contains("system_delta", description);
        Assert.AreEqual("Доставка: system_delta -> planet_iris", MissionUiText.Title(mission, null));
        StringAssert.Contains("planet_iris", MissionUiText.NextStep(mission, null));

        var cargo = ScriptableObject.CreateInstance<ItemConfig>();
        _assets.Add(cargo);
        typeof(BaseConfig).GetField("displayName", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(cargo, "Лекарства");
        IConfigService config = S08_04_ConfigProxy.Make((method, args) =>
            method.Name == "GetItemConfigById" ? cargo : null);
        StringAssert.Contains("Лекарства", MissionUiText.Description(mission, config));
        mission.Status = MissionStatus.Available;
        Assert.AreEqual("Примите миссию", MissionUiText.NextStep(mission, config));
        mission.Status = MissionStatus.ReadyToComplete;
        Assert.AreEqual("Заберите награду", MissionUiText.NextStep(mission, config));
    }

    [Test]
    public void ReconCardUsesSystemAndNoPlanet()
    {
        var mission = new MissionInstanceData
        {
            MissionType = MissionType.Recon,
            Status = MissionStatus.InProgress,
            TargetSystemId = "system_delta",
            TargetPlanetId = "planet_iris",
            SourceSystemId = "system_home",
            SourcePlanetId = "planet_office",
            Objective = new MissionObjectiveData { RequiredAmount = 3, CurrentAmount = 1 }
        };
        StringAssert.Contains("system_delta", MissionUiText.Description(mission, null));
        StringAssert.Contains("Просканируйте 3 корабля", MissionUiText.Description(mission, null));
        StringAssert.DoesNotContain("planet_iris", MissionUiText.Description(mission, null));
        var player = new PlayerState { CurrentSystemId = "system_home" };
        Assert.AreEqual("Летите в систему system_delta.", MissionUiText.NextStep(mission, null, player));

        player.CurrentSystemId = "system_delta";
        mission.Objective.ReconIsInsideTarget = true;
        Assert.IsTrue(MissionUiText.IsReconSurveying(mission, player));
        Assert.AreEqual("Просканируйте 3 корабля. Информация получена об 1 корабле.",
            MissionUiText.NextStep(mission, null, player));

        player.CurrentSystemId = "system_home";
        mission.Objective.ReconIsInsideTarget = false;
        Assert.IsFalse(MissionUiText.IsReconSurveying(mission, player));
        Assert.AreEqual("Летите в систему system_delta.", MissionUiText.NextStep(mission, null, player));

        mission.Objective.CurrentAmount = 3;
        mission.Status = MissionStatus.ReadyToComplete;
        Assert.AreEqual("Вернитесь на планету planet_office системы system_home для получения награды.",
            MissionUiText.NextStep(mission, null, player));
        player.CurrentPlanetId = "planet_office";
        Assert.AreEqual("Заберите награду", MissionUiText.NextStep(mission, null, player));
    }

    [TestCase(1, "1 корабль")]
    [TestCase(2, "2 корабля")]
    [TestCase(5, "5 кораблей")]
    [TestCase(11, "11 кораблей")]
    [TestCase(21, "21 корабль")]
    [TestCase(24, "24 корабля")]
    public void ReconDescriptionDeclinesShipCount(int count, string expected)
    {
        var mission = new MissionInstanceData
        {
            MissionType = MissionType.Recon,
            TargetSystemId = "system_delta",
            Objective = new MissionObjectiveData { RequiredAmount = count }
        };
        StringAssert.Contains("Просканируйте " + expected,
            MissionUiText.Description(mission, null));
    }

    [Test]
    public void RouteUpgradeNextStepChangesAfterDirectSurvey()
    {
        var mission = new MissionInstanceData
        {
            MissionType = MissionType.RouteSearch,
            Status = MissionStatus.InProgress,
            SourceSystemId = "system_home",
            SourcePlanetId = "planet_office",
            TargetSystemId = "system_delta",
            Objective = new MissionObjectiveData { RouteIsUpgrade = true }
        };
        StringAssert.Contains("Галактика нуждается в более дешевых маршрутах перелета.",
            MissionUiText.Description(mission, null));
        mission.Status = MissionStatus.Available;
        Assert.AreEqual("Примите миссию", MissionUiText.NextStep(mission, null));
        mission.Status = MissionStatus.InProgress;
        StringAssert.Contains("прямым маршрутом", MissionUiText.NextStep(mission, null));
        mission.Objective.RouteDirectSurveyed = true;
        Assert.AreEqual("Вернитесь в систему system_home.", MissionUiText.NextStep(mission, null));
        MissionNavigationPlanner.Destination(mission, out string system, out string planet);
        Assert.AreEqual("system_home", system);
        Assert.IsNull(planet);
        mission.Objective.RouteReturnedAfterDirectSurvey = true;
        StringAssert.Contains("через третью систему", MissionUiText.NextStep(mission, null));
        MissionNavigationPlanner.Destination(mission, out system, out planet);
        Assert.AreEqual("system_delta", system);
        Assert.IsNull(planet);
        mission.Objective.RouteIndirectSurveyed = true;
        StringAssert.Contains("planet_office", MissionUiText.NextStep(mission, null));
        MissionNavigationPlanner.Destination(mission, out system, out planet);
        Assert.AreEqual("system_home", system);
        Assert.AreEqual("planet_office", planet);
    }

    [Test]
    public void OfferedMissionMustBeAcceptedBeforeRouteTravel()
    {
        var mission = new MissionInstanceData
        {
            MissionType = MissionType.RouteSearch,
            Status = MissionStatus.Available,
            SourcePlanetId = "planet_office",
            SourceSystemId = "system_home",
            TargetSystemId = "system_delta",
            Objective = new MissionObjectiveData { RouteIsUpgrade = false }
        };
        string offeredStep = MissionUiText.NextStep(mission, null);
        Assert.AreEqual("Примите миссию", offeredStep);

        mission.Status = MissionStatus.Accepted;
        StringAssert.Contains("system_delta", MissionUiText.NextStep(mission, null));
    }

    [Test]
    public void NewRouteSurveyChangesReturnHintAndRewardHintAtIssuerPlanet()
    {
        var mission = new MissionInstanceData
        {
            MissionType = MissionType.RouteSearch,
            Status = MissionStatus.InProgress,
            SourceSystemId = "system_home",
            SourcePlanetId = "planet_office",
            TargetSystemId = "system_delta",
            Objective = new MissionObjectiveData { RouteIsUpgrade = false }
        };
        var player = new PlayerState { CurrentSystemId = "system_delta" };

        Assert.AreEqual("Летите в систему system_delta.",
            MissionUiText.NextStep(mission, null, player));
        MissionNavigationPlanner.Destination(mission, out string system, out _);
        Assert.AreEqual("system_delta", system);

        mission.Objective.RouteIndirectSurveyed = true;
        Assert.AreEqual(
            "Вернитесь на планету planet_office системы system_home для получения награды.",
            MissionUiText.NextStep(mission, null, player));
        MissionNavigationPlanner.Destination(mission, out system, out string planet);
        Assert.AreEqual("system_home", system);
        Assert.AreEqual("planet_office", planet);

        mission.Status = MissionStatus.ReadyToComplete;
        player.CurrentSystemId = "system_home";
        player.CurrentPlanetId = "planet_office";
        Assert.AreEqual("Заберите награду", MissionUiText.NextStep(mission, null, player));
    }

    [Test]
    public void RestoreNoticeOnlyDescribesRealStatusOrNextStepChanges()
    {
        var mission = new MissionInstanceData
        {
            MissionRuntimeId = "route_upgrade",
            Title = "Навигация",
            MissionType = MissionType.RouteSearch,
            Status = MissionStatus.InProgress,
            TargetSystemId = "system_delta",
            SourcePlanetId = "planet_office",
            SourceSystemId = "system_home",
            Objective = new MissionObjectiveData { RouteIsUpgrade = true }
        };
        var block = new RuntimeMissionSaveBlock();
        block.ActiveMissions.Add(mission);
        var loadedState = new GameRuntimeState { MissionBlock = block };
        MissionRestoreChangeDetector.RecordBeforeLoadValidation(loadedState, null);
        var before = MissionRestoreChangeDetector.TakeBeforeRestore(loadedState, null);
        var unchanged = MissionRestoreChangeDetector.Capture(block, null);
        Assert.IsEmpty(MissionRestoreChangeDetector.DescribeFirstChange(before, unchanged));

        mission.Objective.RouteDirectSurveyed = true;
        var newStep = MissionRestoreChangeDetector.Capture(block, null);
        StringAssert.Contains("Вернитесь в систему",
            MissionRestoreChangeDetector.DescribeFirstChange(before, newStep));

        mission.Objective.RouteReturnedAfterDirectSurvey = true;
        var afterReturn = MissionRestoreChangeDetector.Capture(block, null);
        StringAssert.Contains("через третью систему",
            MissionRestoreChangeDetector.DescribeFirstChange(newStep, afterReturn));

        mission.Status = MissionStatus.ReadyToComplete;
        var ready = MissionRestoreChangeDetector.Capture(block, null);
        StringAssert.Contains("награда готова",
            MissionRestoreChangeDetector.DescribeFirstChange(afterReturn, ready));
    }

    [Test]
    public void RawLoadSnapshotRetainsStatusBeforeEarlyNormalization()
    {
        var mission = new MissionInstanceData
        {
            MissionRuntimeId = "legacy",
            Title = "Старое задание",
            MissionType = MissionType.Delivery,
            Status = MissionStatus.InProgress,
            TargetSystemId = "target",
            TargetPlanetId = "planet"
        };
        var state = new GameRuntimeState();
        state.MissionBlock.ActiveMissions.Add(mission);
        MissionRestoreChangeDetector.RecordBeforeLoadValidation(state, null);
        mission.Status = MissionStatus.Cancelled;
        state.MissionBlock.ActiveMissions.Clear();
        state.MissionBlock.ArchivedMissions.Add(mission);

        var before = MissionRestoreChangeDetector.TakeBeforeRestore(state, null);
        var after = MissionRestoreChangeDetector.Capture(state.MissionBlock, null);
        StringAssert.Contains("отменена",
            MissionRestoreChangeDetector.DescribeFirstChange(before, after));
    }

    [Test]
    public void ReadyMissionTargetsRewardLocation()
    {
        var mission = new MissionInstanceData
        {
            MissionType = MissionType.Recon,
            Status = MissionStatus.ReadyToComplete,
            SourceSystemId = "home",
            SourcePlanetId = "office",
            TargetSystemId = "enemy"
        };
        MissionNavigationPlanner.Destination(mission, out string system, out string planet);
        Assert.AreEqual("home", system);
        Assert.AreEqual("office", planet);
    }

    [Test]
    public void RewardButtonRequiresCorrectSystemPlanetAndInhabitedBattlePlanet()
    {
        PlanetConfig inhabited = ScriptableObject.CreateInstance<PlanetConfig>();
        PlanetConfig barren = ScriptableObject.CreateInstance<PlanetConfig>();
        _assets.Add(inhabited);
        _assets.Add(barren);
        SetField(inhabited, "isInhabited", true);
        SetField(barren, "isInhabited", false);
        IConfigService config = S08_04_ConfigProxy.Make((method, args) =>
            method.Name == "GetPlanetConfigById"
                ? (string)args[0] == "colony" ? inhabited : barren
                : null);
        var player = new PlayerState { CurrentSystemId = "target", CurrentPlanetId = "colony" };
        var delivery = new MissionInstanceData
        {
            MissionType = MissionType.Delivery,
            Status = MissionStatus.ReadyToComplete,
            TargetSystemId = "target",
            TargetPlanetId = "colony"
        };
        Assert.IsTrue(MissionCardView.CanTurnInHere(delivery, player, config));
        player.CurrentPlanetId = "barren";
        Assert.IsFalse(MissionCardView.CanTurnInHere(delivery, player, config));
        player.CurrentPlanetId = "colony";
        player.CurrentSystemId = "other";
        Assert.IsFalse(MissionCardView.CanTurnInHere(delivery, player, config));

        var recon = new MissionInstanceData
        {
            MissionType = MissionType.Recon,
            Status = MissionStatus.ReadyToComplete,
            SourceSystemId = "home",
            SourcePlanetId = "office"
        };
        player.CurrentSystemId = "home";
        player.CurrentPlanetId = "office";
        Assert.IsTrue(MissionCardView.CanTurnInHere(recon, player, config));
        player.CurrentSystemId = "target";
        Assert.IsFalse(MissionCardView.CanTurnInHere(recon, player, config));

        var defense = new MissionInstanceData
        {
            MissionType = MissionType.Defense,
            Status = MissionStatus.ReadyToComplete,
            TargetSystemId = "target"
        };
        player.CurrentPlanetId = "colony";
        Assert.IsTrue(MissionCardView.CanTurnInHere(defense, player, config));
        Assert.AreEqual("Защита: target", MissionUiText.Title(defense, config));
        Assert.AreEqual("Заберите награду", MissionUiText.NextStep(defense, config, player));
        player.CurrentPlanetId = "barren";
        Assert.IsFalse(MissionCardView.CanTurnInHere(defense, player, config));
        StringAssert.Contains("ближайшей населённой планете системы target",
            MissionUiText.NextStep(defense, config, player));
        player.CurrentSystemId = "other";
        MissionNavigationPlanner.Destination(defense, out string targetSystem, out string targetPlanet);
        Assert.AreEqual("target", targetSystem);
        Assert.IsNull(targetPlanet);
        player.CurrentSystemId = "target";
        defense.Status = MissionStatus.InProgress;
        player.CurrentPlanetId = "colony";
        Assert.IsFalse(MissionCardView.CanTurnInHere(defense, player, config));
    }

    [Test]
    public void RoutePlannerUsesShortestUnlockedPathAndCanExcludeDirectHop()
    {
        StarSystemConfig a = System("A");
        StarSystemConfig b = System("B");
        StarSystemConfig c = System("C");
        RouteConfig ab = Route("AB", a, b, 2);
        RouteConfig bc = Route("BC", b, c, 3);
        RouteConfig ac = Route("AC", a, c, 9);
        SetField(a, "routes", new List<RouteConfig> { ab, ac });
        SetField(b, "routes", new List<RouteConfig> { ab, bc });
        SetField(c, "routes", new List<RouteConfig> { bc, ac });
        var systems = new List<StarSystemConfig> { a, b, c };
        var galaxy = new GalaxyRuntimeState
        {
            Systems = new List<StarSystemRuntimeState>
            {
                new StarSystemRuntimeState { SystemId = "A", IsDiscovered = true },
                new StarSystemRuntimeState { SystemId = "B", IsDiscovered = true },
                new StarSystemRuntimeState { SystemId = "C", IsDiscovered = true }
            }
        };
        var routes = new TestRoutes("AB", "BC", "AC");

        CollectionAssert.AreEqual(new[] { "A", "B", "C" },
            MissionNavigationPlanner.FindShortestUnlockedPath(systems, galaxy, routes, "A", "C"));
        routes.Lock("BC");
        CollectionAssert.AreEqual(new[] { "A", "C" },
            MissionNavigationPlanner.FindShortestUnlockedPath(systems, galaxy, routes, "A", "C"));
        routes.UnlockRoute("BC");
        CollectionAssert.AreEqual(new[] { "A", "B", "C" },
            MissionNavigationPlanner.FindShortestUnlockedPath(systems, galaxy, routes, "A", "C", true));
        CollectionAssert.AreEqual(new[] { "B", "C" },
            MissionNavigationPlanner.FindShortestUnlockedPath(systems, galaxy, routes,
                "B", "C", true, "A", new List<string> { "A", "B" }));
        routes.Lock("BC");
        Assert.IsEmpty(MissionNavigationPlanner.FindShortestUnlockedPath(systems, galaxy, routes,
            "B", "C", true, "A", new List<string> { "A", "B" }));
    }

    private StarSystemConfig System(string id)
    {
        var asset = ScriptableObject.CreateInstance<StarSystemConfig>();
        SetField(asset, "id", id);
        _assets.Add(asset);
        return asset;
    }

    private RouteConfig Route(string id, StarSystemConfig from, StarSystemConfig to, int parsecs)
    {
        var asset = ScriptableObject.CreateInstance<RouteConfig>();
        SetField(asset, "id", id);
        asset.FromSystem = from;
        asset.ToSystem = to;
        asset.ParsecDistance = parsecs;
        _assets.Add(asset);
        return asset;
    }

    private static void SetField(object target, string name, object value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance |
                BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        Assert.Fail("Missing field " + name);
    }

    private sealed class TestRoutes : IRouteService
    {
        private readonly HashSet<string> _unlocked;
        public TestRoutes(params string[] ids) => _unlocked = new HashSet<string>(ids);
        public bool IsRouteUnlocked(string routeId) => _unlocked.Contains(routeId);
        public bool HasUnlockedRoute(string fromSystemId, string toSystemId) => false;
        public string FindRouteId(string fromSystemId, string toSystemId) => null;
        public void UnlockRoute(string routeId) => _unlocked.Add(routeId);
        public void Lock(string routeId) => _unlocked.Remove(routeId);
    }
}
