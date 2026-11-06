using NUnit.Framework;
using UnityEngine;

public sealed class StationUpgradeDebugEditModeTests
{
    private StationLevelEffectsConfig _levels;
    [SetUp] public void Setup()
    {
        _levels = ScriptableObject.CreateInstance<StationLevelEffectsConfig>();
        _levels.Levels = new[] { new StationLevelEffectsSettings { Level = 2, RequiredGalaxyLevel = 2, MaxHealth = 450 },
            new StationLevelEffectsSettings { Level = 3, RequiredGalaxyLevel = 3, MaxHealth = 500 } };
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(_levels);
    private static StarSystemRuntimeState System() => new StarSystemRuntimeState { SystemId = "sys", Station = new StationRuntimeState {
        ConfigId = "station", Level = 1, HealthVersion = 1, HealthLevel = 1, Health = 400, MaxHealth = 400 } };
    [TestCase(false)] [TestCase(true)]
    public void PlusOne_FinishesUpgradeAndAppliesTargetHealth(bool alreadyStarted)
    {
        var system = System();
        if (alreadyStarted) system.Station.Development = new StationOperationRuntimeState { OperationId = "paid", Kind = StationOperationKind.Upgrade,
            TargetLevel = 2, DurationSeconds = 300, ElapsedSeconds = 10, PaidCosts = new[] { new StationPaymentCostSnapshot { Resource = StationPaymentResource.Credits, Amount = 1000 } } };
        Assert.That(StationUpgradeDebug2A.TryIncrease(system, _levels, 2, false, out _), Is.True);
        Assert.That(system.Station.Level, Is.EqualTo(2)); Assert.That(system.Station.Development, Is.Null);
        Assert.That(system.Station.Health, Is.EqualTo(450)); Assert.That(system.Station.MaxHealth, Is.EqualTo(450));
        string before = JsonUtility.ToJson(system);
        Assert.That(StationUpgradeDebug2A.TryIncrease(system, _levels, 2, false, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("GalaxyLevelRequired")); Assert.That(JsonUtility.ToJson(system), Is.EqualTo(before));
    }
    [TestCase("galaxy", "GalaxyLevelRequired")]
    [TestCase("damaged", "StationDamaged")]
    [TestCase("enemies", "StationUnderAttack")]
    [TestCase("destroyed", "StationDestroyed")]
    [TestCase("unbuilt", "NotBuilt")]
    [TestCase("maximum", "MaximumLevel")]
    public void RejectedUpgrade_DoesNotChangeStation(string condition, string expectedReason)
    {
        var system = System(); int galaxy = condition == "galaxy" ? 1 : 10;
        if (condition == "damaged") system.Station.Health--;
        if (condition == "destroyed") { system.Station.IsDestroyed = true; system.Station.Health = 0; }
        if (condition == "unbuilt") system.Station.Level = 0;
        if (condition == "maximum") system.Station.Level = 10;
        string before = JsonUtility.ToJson(system);
        Assert.That(StationUpgradeDebug2A.TryIncrease(system, _levels, galaxy, condition == "enemies", out string reason), Is.False);
        Assert.That(reason, Is.EqualTo(expectedReason)); Assert.That(JsonUtility.ToJson(system), Is.EqualTo(before));
    }
}
