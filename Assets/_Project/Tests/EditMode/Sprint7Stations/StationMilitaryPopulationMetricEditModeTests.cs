using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationMilitaryPopulationMetricEditModeTests
{
    private readonly List<Object> _assets = new List<Object>();
    private T Asset<T>() where T : ScriptableObject { var result = ScriptableObject.CreateInstance<T>(); _assets.Add(result); return result; }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    [TearDown] public void Cleanup() { foreach (var asset in _assets) Object.DestroyImmediate(asset); _assets.Clear(); }
    [Test] public void AliveCount_UsesBirthStationAcrossSystemsAndExcludesDeadOrOtherSources()
    {
        var own = new SystemNpcRuntimeState { IsAlive = true, NpcType = SystemNpcType.Ally, AllyRole = AllyRole2A.Military,
            CurrentSystemId = "away", SpawnRuleId = StationMilitaryRules.RuleId("home", "base", "old_rule") };
        var dead = new SystemNpcRuntimeState { IsAlive = false, NpcType = SystemNpcType.Ally, AllyRole = AllyRole2A.Military,
            SpawnRuleId = StationMilitaryRules.RuleId("home", "base", "rule") };
        var other = new SystemNpcRuntimeState { IsAlive = true, NpcType = SystemNpcType.Ally, AllyRole = AllyRole2A.Military,
            SpawnRuleId = StationMilitaryRules.RuleId("home", "other_base", "rule") };
        var local = new SystemNpcRuntimeState { IsAlive = true, NpcType = SystemNpcType.Ally, AllyRole = AllyRole2A.Military, SpawnRuleId = "system_rule" };
        Assert.That(StationMilitaryRules.AliveFromStation(new[] { own, dead, other, local }, "home", "base"), Is.EqualTo(1));
    }
    [Test] public void Capacity_UsesGalaxyProfileAndDoesNotDoubleCountSameShipConfigAcrossRules()
    {
        var ally = Asset<AllyConfig>(); Set(ally, "role", AllyRole2A.Military);
        typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ally, "military");
        var rule = Asset<AllySpawnRuleConfig>(); var duplicate = Asset<AllySpawnRuleConfig>();
        var entry = new AllyGroupEntryConfig(); Set(entry, "allyConfig", ally); Set(entry, "maxCount", 3);
        var larger = new AllyGroupEntryConfig(); Set(larger, "allyConfig", ally); Set(larger, "maxCount", 5);
        var first = new AllySpawnLevelEntryConfig(); Set(first, "allies", new[] { entry });
        var second = new AllySpawnLevelEntryConfig(); Set(second, "galaxyLevel", 2); Set(second, "allies", new[] { larger });
        Set(rule, "levelEntries", new[] { first, second }); Set(duplicate, "levelEntries", new[] { first, second });
        var population = Asset<SystemPopulationRule>(); Set(population, "allySpawnRules", new[] { rule, duplicate });
        var settings = new StationLevelEffectsSettings { Population = population };
        Assert.That(StationMilitaryRules.Capacity(settings, 1), Is.EqualTo(3));
        Assert.That(StationMilitaryRules.Capacity(settings, 2), Is.EqualTo(5));
        Assert.That(StationMilitaryRules.Capacity(settings, 3), Is.Zero);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void MilitaryCard_ShowsOwnPopulationInEveryStationState(int state)
    {
        var root = new GameObject("MilitaryMetric");
        try
        {
            var panel = root.AddComponent<SystemStationPanel2A>();
            var station = new StationRuntimeState { StationType = StationType.Military, Level = state < 2 ? 0 : 1,
                IsDestroyed = state == 4, Health = state == 4 ? 0 : 400, MaxHealth = 400 };
            if (state == 1 || state == 3) station.Development = new StationOperationRuntimeState {
                Kind = state == 1 ? StationOperationKind.Construction : StationOperationKind.Upgrade, TargetLevel = state == 1 ? 1 : 2 };
            var card = new StationCardSnapshot { Station = station, MilitaryPopulation = 2, MaximumMilitaryPopulation = 5 };
            string text = (string)typeof(SystemStationPanel2A).GetMethod("Describe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { card });
            Assert.That(text.Split('\n')[state < 2 ? 2 : 3], Is.EqualTo("Популяция военных: 2 / 5"));
        }
        finally { Object.DestroyImmediate(root); }
    }
    [TestCase(StationType.Science)] [TestCase(StationType.Military)]
    public void StationScreen_KeepsGapBetweenMetricAndProgress(StationType type)
    {
        var root = new GameObject("StationMetricGap", typeof(RectTransform));
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2200);
            var view = root.AddComponent<StationResearchPanelView2A>(); view.Build(null);
            view.SetStation(new StationCardSnapshot { Station = new StationRuntimeState { Level = 1, StationType = type,
                Health = 400, MaxHealth = 400, Development = new StationOperationRuntimeState { Kind = StationOperationKind.Upgrade, TargetLevel = 2, DurationSeconds = 30 } },
                MilitaryPopulation = 2, MaximumMilitaryPopulation = 5 }, null);
            var metric = root.transform.Find("ResearchWorkspace/StationInformation/ResearchPower").GetComponent<RectTransform>();
            var button = view.Upgrade.GetComponent<RectTransform>();
            float gap = -button.anchoredPosition.y - (-metric.anchoredPosition.y + metric.rect.height);
            Assert.That(gap, Is.GreaterThanOrEqualTo(24));
            if (type == StationType.Military) Assert.That(metric.GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Популяция военных: 2 / 5"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(1, 1)] [TestCase(2, 2)] [TestCase(3, 3)]
    public void MaximumCapacity_UsesRealAssetsAtGalaxyGateInsteadOfCurrentBaseLevel(int galaxyLevel, int expected)
    {
        var config = UnityEditor.AssetDatabase.LoadAssetAtPath<StationLevelEffectsConfig>(
            "Assets/_Project/Content/Configs/Stations2A/Levels/station_levels_military.asset");
        Assert.That(config, Is.Not.Null);
        Assert.That(StationMilitaryRules.MaximumCapacity(config, galaxyLevel), Is.EqualTo(expected));
        if (galaxyLevel == 2) Assert.That(StationMilitaryRules.Capacity(config.GetLevel(1), galaxyLevel), Is.EqualTo(1));
    }
}
