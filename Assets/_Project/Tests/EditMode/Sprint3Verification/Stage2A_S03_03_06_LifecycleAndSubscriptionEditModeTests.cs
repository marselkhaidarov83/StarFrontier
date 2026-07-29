#if UNITY_EDITOR
using System;
using NUnit.Framework;

public sealed class Stage2A_S03_03_06_LifecycleAndSubscriptionEditModeTests
{
    [TestCase("SystemTopHudFuelIndicator2A")]
    [TestCase("SystemHudSafeAreaAdapter2A")]
    public void HudControllerType_WhenPresent_HasSafeUnityLifecycle(
        string typeName)
    {
        Type type =
            Stage2A_S03_03_TestUtility.FindType(typeName);

        if (type == null)
        {
            Assert.Pass(
                typeName +
                " is not present in this branch. Nothing to verify for this optional controller.");
        }

        bool hasLifecycleEntry =
            Stage2A_S03_03_TestUtility
                .FindParameterlessMethod(
                    type,
                    "Awake",
                    "OnEnable",
                    "Start") != null;

        Assert.IsTrue(
            hasLifecycleEntry,
            typeName +
            " must have Awake, OnEnable, or Start lifecycle entry.");
    }

    [TestCase("SystemTopHudFuelIndicator2A")]
    public void EventSubscriber_WhenPresent_HasCleanupLifecycle(
        string typeName)
    {
        Type type =
            Stage2A_S03_03_TestUtility.FindType(typeName);

        if (type == null)
        {
            Assert.Pass(
                typeName +
                " is not present in this branch. Nothing to verify for this optional controller.");
        }

        bool hasSubscriptionMethod =
            Stage2A_S03_03_TestUtility
                .FindParameterlessMethod(
                    type,
                    "SubscribeEvents") != null;

        if (!hasSubscriptionMethod)
        {
            Assert.Pass(
                typeName +
                " has no explicit SubscribeEvents method.");
        }

        bool hasCleanup =
            Stage2A_S03_03_TestUtility
                .FindParameterlessMethod(
                    type,
                    "OnDisable",
                    "OnDestroy",
                    "UnsubscribeEvents") != null;

        Assert.IsTrue(
            hasCleanup,
            typeName +
            " subscribes to events but has no cleanup lifecycle.");
    }
}
#endif