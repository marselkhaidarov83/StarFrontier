#if UNITY_EDITOR

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_TargetTests
{
    [Test]
    public void TargetingRuntimeState_SelectUpdateClear_Works()
    {
        Type stateType =
            SfTestReflection.RequireType("TargetingRuntimeState");

        Type targetType =
            SfTestReflection.RequireType("SystemGameplayTargetType");

        object state =
            SfTestReflection.CreateInstance(stateType);

        object planet =
            SfTestReflection.EnumValue(
                targetType,
                "Planet");

        MethodInfo setTarget =
            SfTestReflection.RequireMethod(
                stateType,
                "SetTarget",
                7);

        setTarget.Invoke(
            state,
            new object[]
            {
                "planet_test_01",
                planet,
                new Vector2(3f, 4f),
                5f,
                true,
                true,
                true
            });

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "HasTarget"),
            Is.True);

        Assert.That(
            SfTestReflection.GetMemberValue(
                state,
                "CurrentTargetId"),
            Is.EqualTo("planet_test_01"));

        Assert.That(
            SfTestReflection.ToFloat(
                SfTestReflection.GetMemberValue(
                    state,
                    "CurrentTargetDistance")),
            Is.EqualTo(5f).Within(0.0001f));

        SfTestReflection.Invoke(
            state,
            "UpdateTargetDistance",
            -10f,
            false);

        Assert.That(
            SfTestReflection.ToFloat(
                SfTestReflection.GetMemberValue(
                    state,
                    "CurrentTargetDistance")),
            Is.EqualTo(0f));

        SfTestReflection.Invoke(
            state,
            "ClearTarget");

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "HasTarget"),
            Is.False);

        Assert.That(
            SfTestReflection.GetMemberValue(
                state,
                "CurrentTargetId"),
            Is.EqualTo(string.Empty));
    }

    [Test]
    public void TargetService_Contract_IsComplete()
    {
        Type interfaceType =
            SfTestReflection.RequireType("ITargetService2A");

        Type serviceType =
            SfTestReflection.RequireType("TargetService2A");

        Assert.That(
            interfaceType.IsAssignableFrom(serviceType),
            Is.True);

        RequireNamedMethod(interfaceType, "TrySelectTarget");
        RequireNamedMethod(interfaceType, "TryRefreshCurrentTarget");
        RequireNamedMethod(interfaceType, "IsCurrentTarget");
        RequireNamedMethod(interfaceType, "ClearTarget");

        Type failType =
            SfTestReflection.RequireType(
                "TargetSelectionFailReason2A");

        SfTestReflection.AssertEnumContains(
            failType,
            "None",
            "ServiceNotReady",
            "EmptyTargetId",
            "InvalidTargetType",
            "InvalidWorldPosition",
            "TargetUnavailable",
            "TargetTypeDisabled",
            "TargetIsNotCurrent");
    }

    [Test]
    public void TargetService_UsesExistingGameplayRuntimeState()
    {
        Type serviceType =
            SfTestReflection.RequireType("TargetService2A");

        Type gameplayStateInterface =
            SfTestReflection.RequireType(
                "ISystemGameplayStateService");

        bool hasDependency =
            serviceType
                .GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Any(field =>
                    field.FieldType ==
                    gameplayStateInterface);

        Assert.That(
            hasDependency,
            Is.True,
            "TargetService2A must use ISystemGameplayStateService instead of creating a second target state.");
    }

    [Test]
    public void TargetChangedEvent_ExistsAndCarriesStableIdentifier()
    {
        Type eventType =
            SfTestReflection.RequireType(
                "TargetChangedEvent2A");

        string[] expectedMembers =
        {
            "TargetId",
            "TargetType",
            "HasTarget"
        };

        foreach (string member in expectedMembers)
        {
            bool found =
                eventType.GetMember(
                    member,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Length > 0;

            Assert.That(
                found,
                Is.True,
                "TargetChangedEvent2A must expose " + member + ".");
        }
    }

    [Test]
    public void TargetingConfig_ContainsSelectionAndVisualSettings()
    {
        Type configType =
            SfTestReflection.RequireType("TargetingConfig");

        string[] requiredProperties =
        {
            "MaxTargetDistance",
            "TapSelectRadiusWorld",
            "AutoSelectRadiusWorld",
            "PreferInteractableTargets",
            "AllowPlanets",
            "AllowStations",
            "AllowTravelPoints",
            "AllowEnemies",
            "MarkerWorldScale",
            "MarkerPulseSpeed",
            "UnavailableTargetAlpha"
        };

        foreach (string propertyName in requiredProperties)
        {
            PropertyInfo property =
                configType.GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            Assert.That(
                property,
                Is.Not.Null,
                "TargetingConfig is missing property " +
                propertyName + ".");
        }
    }

    private static void RequireNamedMethod(
        Type type,
        string methodName)
    {
        Assert.That(
            type.GetMethods()
                .Any(method =>
                    method.Name == methodName),
            Is.True,
            type.Name + " must contain method " +
            methodName + ".");
    }
}

#endif
