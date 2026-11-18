using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class S08_02_MissionContentEditModeTests
{
    private const string Root = "Assets/_Project/Content/Configs/";

    [Test]
    public void RouteAssets_HaveTenPositiveFuelLevels()
    {
        var guids = AssetDatabase.FindAssets("t:RouteConfig", new[] { Root + "Galaxy/Routes" });
        Assert.That(guids.Length, Is.GreaterThan(0));
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var route = AssetDatabase.LoadAssetAtPath<RouteConfig>(path);
            Assert.That(route.FuelByDevelopmentLevel, Has.Count.EqualTo(RouteConfig.MaxDevelopmentLevel), path);
            Assert.That(route.FuelByDevelopmentLevel[0], Is.GreaterThan(0), path);
            for (int level = 6; level <= RouteConfig.MaxDevelopmentLevel; level++)
                Assert.That(route.GetFuelCost(level), Is.EqualTo(Math.Max(1, route.GetFuelCost(level - 1) - 1)), path + " level " + level);
        }
    }

    [Test]
    public void MissionPools_ContainDeliveryReconAndBothRouteVariants()
    {
        var guids = AssetDatabase.FindAssets("t:MissionPoolConfig", new[] { Root + "Mission/MissionPool" });
        Assert.That(guids.Length, Is.EqualTo(10));
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var pool = AssetDatabase.LoadAssetAtPath<MissionPoolConfig>(path);
            Assert.That(pool, Is.Not.Null, path);
            Assert.That(pool.PossibleMissionOffers, Has.Count.EqualTo(4), path);
            Assert.That(pool.PossibleMissionOffers.All(e => e != null && e.IsEnabled && e.Weight > 0), Is.True, path);

            var templates = pool.PossibleMissionOffers.Select(e => e.MissionTemplateConfig).ToList();
            Assert.That(templates.OfType<DeliveryMissionTemplateConfig>().Count(), Is.EqualTo(1), path);
            Assert.That(templates.OfType<ReconMissionTemplateConfig>().Count(), Is.EqualTo(1), path);
            Assert.That(templates.OfType<RouteMissionTemplateConfig>().Count(t => !t.ImproveExistingRoute), Is.EqualTo(1), path);
            Assert.That(templates.OfType<RouteMissionTemplateConfig>().Count(t => t.ImproveExistingRoute), Is.EqualTo(1), path);
            foreach (var route in templates.OfType<RouteMissionTemplateConfig>())
            {
                Assert.That(route.CreditRewardMode, Is.EqualTo(MissionCreditRewardMode.Fixed), path);
                Assert.That(route.RewardCredits, Is.GreaterThanOrEqualTo(0), path);
            }
        }
    }

    [Test]
    public void MissionGameplayConfig_HasBattleRewardSettings()
    {
        var gameplay = AssetDatabase.LoadAssetAtPath<MissionGameplayConfig>(Root + "Mission/MissionTemplates/mission_gameplay_01.asset");
        Assert.That(gameplay, Is.Not.Null);
        Assert.That(gameplay.DefenseKillRewardDeflator, Is.GreaterThanOrEqualTo(0));
        Assert.That(gameplay.LiberationKillRewardDeflator, Is.GreaterThanOrEqualTo(0));
    }
}
