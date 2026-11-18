using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public sealed class PopulationSpawnSeparationEditModeTests
{
    [Test]
    public void StationPopulationProfiles_UseSharedConfigWithTenLevelsAndNoInvasion()
    {
        const string root = "Assets/_Project/Content/Configs/Galaxy/Stations/StationType";
        var types = AssetDatabase.FindAssets("t:StationTypeConfig", new[] { root })
            .Select(guid => AssetDatabase.LoadAssetAtPath<StationTypeConfig>(AssetDatabase.GUIDToAssetPath(guid)))
            .ToArray();
        Assert.That(types.Length, Is.EqualTo(5));
        Assert.That(types.Select(type => type.Population).Distinct().Count(), Is.EqualTo(types.Length));
        foreach (var type in types)
        {
            var population = type.Population;
            Assert.That(population, Is.Not.Null, type.Id);
            Assert.That(population.StationLevels.Select(level => level.Level), Is.EqualTo(Enumerable.Range(1, 10)));
            Assert.That(new SerializedObject(population).FindProperty("invasionSpawnRuleSet").objectReferenceValue,
                Is.Null, type.Id);
        }
    }

    [Test]
    public void AlliedPopulationProfiles_UseSeparateInvasionAsset_WithoutLegacySpawnArrays()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:PopulationConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("/Population/SystemPopulationRules/"))
                continue;
            var profile = AssetDatabase.LoadAssetAtPath<PopulationConfig>(path);
            Assert.That(profile.AllySpawnRules.Any(rule => rule != null), Is.True, path);
            var legacy = (SystemPopulationEnemyGroupRuleEntry[])typeof(PopulationConfig)
                .GetField("enemyGroupSpawnRuleEntries", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(profile);
            Assert.That(legacy, Is.Empty, path);
            if (path.Contains("no_enemies"))
                Assert.That(profile.InvasionSpawnRuleSet, Is.Null, path);
            else
            {
                Assert.That(profile.InvasionSpawnRuleSet, Is.Not.Null, path);
                Assert.That(profile.InvasionSpawnRuleSet.Entries.Length, Is.EqualTo(3), path);
                foreach (var entry in profile.InvasionSpawnRuleSet.Entries)
                    Assert.That(entry != null && entry.IsValid(), Is.True, path);
            }
        }
    }

    [Test]
    public void InvasionGroupSelection_MatchesTheActualRequestedFaction()
    {
        var set = AssetDatabase.LoadAssetAtPath<InvasionSpawnRuleSetConfig>(
            "Assets/_Project/Content/Configs/Population/invasionSpawnRules_01.asset");
        Assert.That(set, Is.Not.Null);
        foreach (var entry in set.Entries)
        {
            foreach (EnemyFaction2A faction in System.Enum.GetValues(typeof(EnemyFaction2A)))
            {
                for (int level = 1; level <= 10; level++)
                {
                    var enemies = entry.EnemyGroupSpawnRule.PickEnemiesForGalaxyLevel(level, faction);
                    foreach (var enemy in enemies)
                    {
                        string prefix = faction == EnemyFaction2A.Ancients ? "enemy_ancients_"
                            : faction == EnemyFaction2A.Infected ? "enemy_infected_" : "enemy_ai_";
                        Assert.That(enemy.EnemyConfig.Id.StartsWith(prefix), Is.True);
                    }
                }
            }
        }
    }

    [TestCase(InvasionLifecycleState.Preparing)]
    [TestCase(InvasionLifecycleState.Active)]
    public void UnfinishedInvasion_BlocksOnlyItsOwnSystem_WithinTheGlobalLimit(InvasionLifecycleState state)
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Invasions.Add(new InvasionState { InvasionId = "one", TargetSystemId = "a", LifecycleState = state });
        Assert.That(galaxy.CanStartAdditionalInvasion("a", 2), Is.False);
        Assert.That(galaxy.CanStartAdditionalInvasion("b", 2), Is.True);
        Assert.That(galaxy.CanStartAdditionalInvasion("b", 1), Is.False);
        galaxy.Invasions[0].LifecycleState = InvasionLifecycleState.Resolved;
        Assert.That(galaxy.CanStartAdditionalInvasion("a", 2), Is.True);
    }
}
