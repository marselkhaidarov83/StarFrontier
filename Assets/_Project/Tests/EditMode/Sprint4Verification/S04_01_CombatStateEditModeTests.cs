using System.Reflection;
using NUnit.Framework;

public sealed class S04_01_CombatStateEditModeTests
{
    [Test]
    public void ActiveSystemEncounter_StoresCanonicalEncounterState()
    {
        ActiveSystemEncounter encounter =
            new ActiveSystemEncounter
            {
                EncounterId = "encounter_test_01",
                SystemId = "system_test_01",
                State = SystemEncounterState.Active,
                EnemiesAlive = 3,
                AlliesAlive = 2,
                PlayerKills = 0,
                PlayerDestroyed = false,
                PlayerIsOnPlanet = false,
                DaysPlayerStayedOnPlanetDuringEncounter = 0,
                DefeatReason = SystemEncounterDefeatReason.None
            };

        Assert.AreEqual("encounter_test_01", encounter.EncounterId);
        Assert.AreEqual("system_test_01", encounter.SystemId);
        Assert.AreEqual(SystemEncounterState.Active, encounter.State);

        Assert.IsTrue(encounter.HasAliveEnemies);
        Assert.IsTrue(encounter.HasAliveAllies);
        Assert.IsTrue(encounter.IsActive);
        Assert.IsFalse(encounter.IsDefeated);
        Assert.IsFalse(encounter.IsResolved);
        Assert.IsFalse(encounter.CanClaimGovernmentReward);
    }

    [Test]
    public void ActiveSystemEncounter_ExposesRequiredCombatStateFields()
    {
        AssertFieldExists<string>("EncounterId");
        AssertFieldExists<string>("SystemId");
        AssertFieldExists<SystemEncounterState>("State");

        AssertFieldExists<int>("EnemiesAlive");
        AssertFieldExists<int>("AlliesAlive");

        AssertFieldExists<int>("PlayerKills");
        AssertFieldExists<bool>("PlayerDestroyed");

        AssertFieldExists<bool>("PlayerIsOnPlanet");
        AssertFieldExists<int>("DaysPlayerStayedOnPlanetDuringEncounter");

        AssertFieldExists<SystemEncounterDefeatReason>("DefeatReason");
    }

    [Test]
    public void ActiveSystemEncounter_ComputedFlagsMatchState()
    {
        ActiveSystemEncounter encounter =
            new ActiveSystemEncounter
            {
                State = SystemEncounterState.VictoryPendingReward,
                EnemiesAlive = 0,
                AlliesAlive = 1,
                PlayerKills = 2,
                PlayerDestroyed = false
            };

        Assert.IsFalse(encounter.HasAliveEnemies);
        Assert.IsTrue(encounter.HasAliveAllies);
        Assert.IsTrue(encounter.PlayerParticipated);
        Assert.IsFalse(encounter.IsActive);
        Assert.IsTrue(encounter.IsVictoryPendingReward);
        Assert.IsTrue(encounter.CanClaimGovernmentReward);
    }

    private static void AssertFieldExists<TField>(
        string fieldName)
    {
        FieldInfo field =
            typeof(ActiveSystemEncounter).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public);

        Assert.NotNull(
            field,
            "ActiveSystemEncounter must expose field: " + fieldName);

        Assert.AreEqual(
            typeof(TField),
            field.FieldType,
            "ActiveSystemEncounter field has wrong type: " + fieldName);
    }
}