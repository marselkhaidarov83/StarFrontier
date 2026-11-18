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
    public void SharedMissionPool_ContainsDeliveryReconAndBothRouteVariants()
    {
        var pool = AssetDatabase.LoadAssetAtPath<PlanetMissionConfig>(Root + "PlanetMission/planetMissionConfig_shared_01.asset");
        Assert.That(pool, Is.Not.Null);
        var templates = pool.PossibleMissionOffers.Where(e => e != null).Select(e => e.MissionTemplateConfig).ToList();
        Assert.That(templates.OfType<DeliveryMissionTemplateConfig>().Any(), Is.True);
        Assert.That(templates.OfType<ReconMissionTemplateConfig>().Any(), Is.True);
        Assert.That(templates.OfType<RouteMissionTemplateConfig>().Any(t => !t.ImproveExistingRoute), Is.True);
        Assert.That(templates.OfType<RouteMissionTemplateConfig>().Any(t => t.ImproveExistingRoute), Is.True);
        foreach (var route in templates.OfType<RouteMissionTemplateConfig>())
        {
            Assert.That(route.CreditRewardMode, Is.EqualTo(MissionCreditRewardMode.Fixed));
            Assert.That(route.RewardCredits, Is.GreaterThanOrEqualTo(0));
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
