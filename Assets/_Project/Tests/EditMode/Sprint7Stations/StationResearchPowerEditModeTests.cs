using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationResearchPowerEditModeTests
{
    private readonly List<Object> _assets = new List<Object>();
    private T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); _assets.Add(asset); return asset; }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Id(BaseConfig target, string id) =>
        typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, id);
    [TearDown] public void Cleanup() { foreach (var asset in _assets) Object.DestroyImmediate(asset); _assets.Clear(); }
    [Test] public void Maximum_UsesReachableLevelsInUnlockedSectorsIncludingUnbuiltSlots()
    {
        var levels = Asset<StationLevelEffectsConfig>();
        levels.Levels = new[] { new StationLevelEffectsSettings { Level = 1, ScienceRate = 2 },
            new StationLevelEffectsSettings { Level = 2, RequiredGalaxyLevel = 2, ScienceRate = 5 },
            new StationLevelEffectsSettings { Level = 3, RequiredGalaxyLevel = 3, ScienceRate = 9 } };
        var type = Asset<StationTypeConfig>(); Set(type, "stationType", StationType.Science);
        var station = Asset<StationConfig>(); Set(station, "stationTypeConfig", type); Set(station, "levelEffects", levels);
        var firstSystem = Asset<StarSystemConfig>(); Id(firstSystem, "first"); Set(firstSystem, "station", station);
        var secondSystem = Asset<StarSystemConfig>(); Id(secondSystem, "second"); Set(secondSystem, "station", station);
        var first = Asset<SectorConfig>(); Id(first, "a"); Set(first, "systems", new[] { firstSystem });
        var second = Asset<SectorConfig>(); Id(second, "b"); Set(second, "systems", new[] { secondSystem });
        var galaxy = new GalaxyRuntimeState();
        galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "a", IsUnlocked = true });
        galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "b" });
        Assert.That(StationResearchPowerRules.Maximum(galaxy, new[] { first, second }), Is.EqualTo(2));
        galaxy.Sectors[1].IsUnlocked = true;
        Assert.That(StationResearchPowerRules.Maximum(galaxy, new[] { first, second }), Is.EqualTo(10));
        levels.Levels[1].RequiredGalaxyLevel = 3; levels.Levels[2].RequiredGalaxyLevel = 1;
        Assert.That(StationResearchPowerRules.Maximum(galaxy, new[] { first, second }), Is.EqualTo(4));
    }
    [TestCase(StationOperationKind.Construction)] [TestCase(StationOperationKind.Upgrade)]
    public void Development_StopsForEnemiesAndResumesWithoutSkippingTime(StationOperationKind kind)
    {
        var system = new StarSystemRuntimeState { Station = new StationRuntimeState { Level = kind == StationOperationKind.Upgrade ? 1 : 0,
            HealthVersion = 1, HealthLevel = kind == StationOperationKind.Upgrade ? 1 : 0, Health = 400, MaxHealth = 400,
            Development = new StationOperationRuntimeState { OperationId = "op", Kind = kind, TargetLevel = kind == StationOperationKind.Upgrade ? 2 : 1,
                DurationSeconds = 100, ElapsedSeconds = 25 } } };
        StationLifecycleRules.Advance(system, 10, _ => 400, true);
        Assert.That(system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.UnderAttack));
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(25));
        StationLifecycleRules.Advance(system, 10, _ => 400, false);
        Assert.That(system.Station.Development.ElapsedSeconds, Is.EqualTo(35));
        Assert.That(system.Station.Development.PauseReason, Is.EqualTo(StationOperationPauseReason.None));
    }

    [TestCase(false)] [TestCase(true)]
    public void ScienceStationView_ShowsAggregatePowerAtTopRight(bool upgrading)
    {
        var root = new GameObject("SciencePowerView", typeof(RectTransform));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2200);
            var view = root.AddComponent<StationResearchPanelView2A>(); view.Build(null);
            var station = new StationRuntimeState { Level = 1, StationType = StationType.Science, Health = 400, MaxHealth = 400 };
            if (upgrading) station.Development = new StationOperationRuntimeState { Kind = StationOperationKind.Upgrade,
                TargetLevel = 2, DurationSeconds = 30, ElapsedSeconds = 15 };
            view.SetStation(new StationCardSnapshot { Station = station, GalaxyLevel = 2, ResearchPower = 3, MaximumResearchPower = 20 }, null);
            var power = root.transform.Find("ResearchWorkspace/StationInformation/ResearchPower");
            var health = root.transform.Find("ResearchWorkspace/StationInformation/StationHealth");
            Assert.That(power.gameObject.activeSelf, Is.True);
            Assert.That(power.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Сила исследований: 3 / 20"));
            var powerRect = power.GetComponent<RectTransform>();
            Assert.That(powerRect.anchoredPosition.y, Is.GreaterThan(health.GetComponent<RectTransform>().anchoredPosition.y));
            var info = (RectTransform)power.parent;
            Assert.That(info.rect.width - powerRect.anchoredPosition.x - powerRect.rect.width, Is.EqualTo(18).Within(.01));
            Assert.That(power.GetComponent<TMPro.TMP_Text>().alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Right));
            station.StationType = StationType.Trade;
            view.SetStation(new StationCardSnapshot { Station = station }, null);
            Assert.That(power.gameObject.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void ScienceOverview_ShowsGlobalPowerInRequestedStateLine(int state)
    {
        var root = new GameObject("PowerOverview");
        try
        {
            var panel = root.AddComponent<SystemStationPanel2A>();
            var station = new StationRuntimeState { StationType = StationType.Science, Level = state == 2 ? 1 : 0,
                IsDestroyed = state == 2, Health = 0, MaxHealth = 400 };
            if (state == 1) station.Development = new StationOperationRuntimeState { Kind = StationOperationKind.Construction, TargetLevel = 1 };
            var card = new StationCardSnapshot { Station = station, ResearchPower = 3, MaximumResearchPower = 20 };
            var describe = typeof(SystemStationPanel2A).GetMethod("Describe", BindingFlags.Instance | BindingFlags.NonPublic);
            string[] lines = ((string)describe.Invoke(panel, new object[] { card })).Split('\n');
            Assert.That(lines[state == 2 ? 3 : 2], Is.EqualTo("Сила исследований: 3 / 20"));
            if (state == 2) Assert.That(lines[2], Is.EqualTo("Здоровье: 0 / 400"));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
