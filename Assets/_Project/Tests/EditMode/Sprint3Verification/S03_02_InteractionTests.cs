#if UNITY_EDITOR

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
[Category("S03_02")]
public sealed class S03_02_InteractionTests
{
    [Test]
    public void InteractionRuntimeState_PositiveAndNegativeTransitions_Work()
    {
        Type stateType =
            SfTestReflection.RequireType(
                "InteractionRuntimeState");

        Type targetType =
            SfTestReflection.RequireType(
                "SystemGameplayTargetType");

        object state =
            SfTestReflection.CreateInstance(stateType);

        object planet =
            SfTestReflection.EnumValue(
                targetType,
                "Planet");

        SfTestReflection.Invoke(
            state,
            "SetAvailableInteraction",
            "planet_01",
            planet,
            true,
            string.Empty);

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "CanInteract"),
            Is.True);

        SfTestReflection.Invoke(
            state,
            "MarkPressedThisFrame");

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "InteractionPressedThisFrame"),
            Is.True);

        SfTestReflection.Invoke(
            state,
            "SetInteractionInProgress",
            true,
            2f);

        Assert.That(
            SfTestReflection.ToFloat(
                SfTestReflection.GetMemberValue(
                    state,
                    "HoldProgressNormalized")),
            Is.EqualTo(1f));

        SfTestReflection.Invoke(
            state,
            "MarkCompleted");

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "InteractionCompletedThisFrame"),
            Is.True);

        SfTestReflection.Invoke(
            state,
            "MarkFailed",
            "out_of_range");

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "InteractionFailedThisFrame"),
            Is.True);

        Assert.That(
            SfTestReflection.GetMemberValue(
                state,
                "FailReason"),
            Is.EqualTo("out_of_range"));

        SfTestReflection.Invoke(
            state,
            "ResetFrameFlags");

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "InteractionPressedThisFrame"),
            Is.False);

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "InteractionCompletedThisFrame"),
            Is.False);

        Assert.That(
            (bool)SfTestReflection.GetMemberValue(
                state,
                "InteractionFailedThisFrame"),
            Is.False);
    }

    [Test]
    public void InteractionRuntimeState_CooldownNeverBecomesNegative()
    {
        Type stateType =
            SfTestReflection.RequireType(
                "InteractionRuntimeState");

        object state =
            SfTestReflection.CreateInstance(stateType);

        SfTestReflection.Invoke(
            state,
            "SetCooldown",
            2f);

        SfTestReflection.Invoke(
            state,
            "TickCooldown",
            5f);

        Assert.That(
            SfTestReflection.ToFloat(
                SfTestReflection.GetMemberValue(
                    state,
                    "CooldownRemainingSeconds")),
            Is.EqualTo(0f));
    }

    [Test]
    public void InteractionService_Contract_Exists()
    {
        Type interfaceType =
            SfTestReflection.RequireType(
                "IInteractionService2A");

        Type serviceType =
            SfTestReflection.RequireType(
                "InteractionService2A");

        Assert.That(
            interfaceType.IsAssignableFrom(
                serviceType),
            Is.True);

        AssertNamedMethod(interfaceType, "CanInteract");
        AssertNamedMethod(interfaceType, "GetFailReason");
        AssertNamedMethod(interfaceType, "Execute");
    }

    [Test]
    public void InteractionFailReason_ContainsRequiredNegativePaths()
    {
        Type failType =
            SfTestReflection.RequireType(
                "InteractionFailReason2A");

        SfTestReflection.AssertEnumContains(
            failType,
            "None",
            "NoTarget",
            "TargetUnavailable",
            "OutOfRange",
            "UnsupportedTargetType",
            "RequirementsNotMet",
            "InsufficientFuel",
            "ExecutionFailed");
    }

    [Test]
    public void InteractionService_DoesNotOwnSaveTransaction()
    {
        Type serviceType =
            SfTestReflection.RequireType(
                "InteractionService2A");

        MethodInfo execute =
            serviceType.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "Execute");

        Assert.That(execute, Is.Not.Null);

        Assert.That(
            SfTestReflection.MethodReferences(
                execute,
                "SaveNeedEvent"),
            Is.False,
            "InteractionService2A must delegate gameplay action to the domain service; it must not save independently.");
    }

    private static void AssertNamedMethod(
        Type type,
        string methodName)
    {
        Assert.That(
            type.GetMethods()
                .Any(method =>
                    method.Name == methodName),
            Is.True,
            type.Name + " must contain " +
            methodName + ".");
    }
}

#endif
