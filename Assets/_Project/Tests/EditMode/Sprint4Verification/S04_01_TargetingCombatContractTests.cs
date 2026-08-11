using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class S04_01_TargetingCombatContractTests
{
    [Test]
    public void SystemGameplayTargetType_ContainsCombatTargets()
    {
        Assert.IsTrue(
            System.Enum.IsDefined(
                typeof(SystemGameplayTargetType),
                SystemGameplayTargetType.Enemy),
            "SystemGameplayTargetType must contain Enemy.");

        Assert.IsTrue(
            System.Enum.IsDefined(
                typeof(SystemGameplayTargetType),
                SystemGameplayTargetType.Ally),
            "SystemGameplayTargetType must contain Ally.");
    }

    [Test]
    public void TargetService_ExposesSelectionRefreshAndClearContract()
    {
        AssertMethodExists(
            typeof(ITargetService2A),
            "TrySelectTarget",
            typeof(string),
            typeof(SystemGameplayTargetType),
            typeof(Vector2),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(TargetSelectionFailReason2A).MakeByRefType());

        AssertMethodExists(
            typeof(ITargetService2A),
            "TryRefreshCurrentTarget",
            typeof(string),
            typeof(Vector2),
            typeof(bool),
            typeof(bool),
            typeof(TargetSelectionFailReason2A).MakeByRefType());

        AssertMethodExists(
            typeof(ITargetService2A),
            "IsCurrentTarget",
            typeof(string));

        AssertMethodExists(
            typeof(ITargetService2A),
            "ClearTarget");
    }

    [Test]
    public void TargetingRuntimeState_StoresSelectedCombatTargetSnapshot()
    {
        TargetingRuntimeState state =
            new TargetingRuntimeState();

        state.SetTarget(
            "enemy_runtime_01",
            SystemGameplayTargetType.Enemy,
            new Vector2(4f, 5f),
            6f,
            true,
            true,
            true);

        Assert.IsTrue(state.HasTarget);
        Assert.AreEqual("enemy_runtime_01", state.CurrentTargetId);
        Assert.AreEqual(SystemGameplayTargetType.Enemy, state.CurrentTargetType);
        Assert.AreEqual(new Vector2(4f, 5f), state.CurrentTargetWorldPosition);
        Assert.AreEqual(6f, state.CurrentTargetDistance);
        Assert.IsTrue(state.IsTargetInteractable);
        Assert.IsTrue(state.IsTargetInRange);
        Assert.IsTrue(state.WasSelectedByPlayer);

        state.ClearTarget();

        Assert.IsFalse(state.HasTarget);
        Assert.AreEqual(string.Empty, state.CurrentTargetId);
        Assert.AreEqual(SystemGameplayTargetType.None, state.CurrentTargetType);
    }

    [Test]
    public void TargetChangedEvent_CarriesCombatTargetPayload()
    {
        TargetChangedEvent2A evt =
            new TargetChangedEvent2A(
                "ally_runtime_01",
                SystemGameplayTargetType.Ally,
                true);

        Assert.AreEqual("ally_runtime_01", evt.TargetId);
        Assert.AreEqual(SystemGameplayTargetType.Ally, evt.TargetType);
        Assert.IsTrue(evt.HasTarget);
    }

    private static void AssertMethodExists(
        System.Type type,
        string methodName,
        params System.Type[] parameterTypes)
    {
        MethodInfo method =
            type.GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public,
                null,
                parameterTypes,
                null);

        Assert.NotNull(
            method,
            type.Name + " must expose method: " + methodName);
    }
}