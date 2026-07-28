#if UNITY_EDITOR

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_SaveContractTests
{
    [Test]
    public void OneShipRuntimeType_OwnsCurrentFuelAndCapacity()
    {
        Type[] candidates =
            SfTestReflection.AllTypes()
                .Where(type =>
                    type.Name.Contains(
                        "Ship",
                        StringComparison.OrdinalIgnoreCase) &&
                    HasMember(type, "CurrentFuel") &&
                    HasMember(type, "FuelCapacity"))
                .ToArray();

        Assert.That(
            candidates.Length,
            Is.GreaterThanOrEqualTo(1),
            "No ship runtime/state type with CurrentFuel and FuelCapacity was found.");

        Type[] persistentCandidates =
            candidates
                .Where(type =>
                    type.IsSerializable ||
                    type.Name.Contains(
                        "RuntimeData",
                        StringComparison.OrdinalIgnoreCase) ||
                    type.Name.Contains(
                        "State",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        Assert.That(
            persistentCandidates.Length,
            Is.GreaterThanOrEqualTo(1),
            "Fuel owner must be a serializable ship runtime/state type.");
    }

    [Test]
    public void FuelOwner_JsonRoundTrip_PreservesValues()
    {
        Type fuelOwnerType =
            SfTestReflection.AllTypes()
                .FirstOrDefault(type =>
                    type.Name.Contains(
                        "Ship",
                        StringComparison.OrdinalIgnoreCase) &&
                    HasMember(type, "CurrentFuel") &&
                    HasMember(type, "FuelCapacity") &&
                    (type.IsSerializable ||
                     type.Name.Contains(
                         "RuntimeData",
                         StringComparison.OrdinalIgnoreCase)));

        Assert.That(
            fuelOwnerType,
            Is.Not.Null,
            "Serializable ship fuel owner was not found.");

        object source =
            SfTestReflection.CreateInstance(
                fuelOwnerType);

        SfTestReflection.SetMemberValue(
            source,
            37,
            "CurrentFuel");

        SfTestReflection.SetMemberValue(
            source,
            90,
            "FuelCapacity");

        string json =
            JsonUtility.ToJson(source);

        Assert.That(
            string.IsNullOrWhiteSpace(json),
            Is.False);

        object restored =
            JsonUtility.FromJson(
                json,
                fuelOwnerType);

        Assert.That(restored, Is.Not.Null);

        Assert.That(
            SfTestReflection.ToInt(
                SfTestReflection.GetMemberValue(
                    restored,
                    "CurrentFuel")),
            Is.EqualTo(37));

        Assert.That(
            SfTestReflection.ToInt(
                SfTestReflection.GetMemberValue(
                    restored,
                    "FuelCapacity")),
            Is.EqualTo(90));
    }

    [Test]
    public void TargetRuntimeState_IsNotEmbeddedInPersistentPlayerOrGameState()
    {
        Type targetStateType =
            SfTestReflection.RequireType(
                "TargetingRuntimeState");

        string[] persistentTypeNames =
        {
            "GameState",
            "PlayerState",
            "PlayerShipState",
            "ShipRuntimeData"
        };

        foreach (string persistentName in persistentTypeNames)
        {
            Type persistentType =
                SfTestReflection.FindType(
                    persistentName);

            if (persistentType == null)
                continue;

            Assert.That(
                SfTestReflection
                    .TypeHasDirectMemberOfType(
                        persistentType,
                        targetStateType),
                Is.False,
                persistentName +
                " must not serialize TargetingRuntimeState.");
        }
    }

    [Test]
    public void SystemGameplayRuntimeState_IsExplicitlyRuntimeOnly()
    {
        Type runtimeType =
            SfTestReflection.RequireType(
                "SystemGameplayRuntimeState");

        Type targetStateType =
            SfTestReflection.RequireType(
                "TargetingRuntimeState");

        Assert.That(
            SfTestReflection
                .TypeHasDirectMemberOfType(
                    runtimeType,
                    targetStateType),
            Is.True,
            "TargetingRuntimeState must belong to SystemGameplayRuntimeState.");
    }

    private static bool HasMember(
        Type type,
        string memberName)
    {
        return type.GetMember(
                memberName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Length > 0;
    }
}

#endif
