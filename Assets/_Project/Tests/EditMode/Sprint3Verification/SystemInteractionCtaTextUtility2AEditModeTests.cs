using NUnit.Framework;

public sealed class SystemInteractionCtaTextUtility2AEditModeTests
{
    [Test]
    public void GetButtonText_ReturnsAvailableText()
    {
        string result =
            SystemInteractionCtaTextUtility2A
                .GetButtonText(
                    SystemInteractionCtaState2A.Available);

        Assert.AreEqual(
            "Действие",
            result);
    }

    [Test]
    public void GetButtonText_ReturnsNoTargetText()
    {
        string result =
            SystemInteractionCtaTextUtility2A
                .GetButtonText(
                    SystemInteractionCtaState2A.NoTarget);

        Assert.AreEqual(
            "Нет цели",
            result);
    }

    [Test]
    public void GetButtonText_ReturnsOutOfRangeText()
    {
        string result =
            SystemInteractionCtaTextUtility2A
                .GetButtonText(
                    SystemInteractionCtaState2A.OutOfRange);

        Assert.AreEqual(
            "Далеко",
            result);
    }

    [Test]
    public void GetReasonText_ReturnsNoTargetReason()
    {
        string result =
            SystemInteractionCtaTextUtility2A
                .GetReasonText(
                    SystemInteractionCtaState2A.NoTarget);

        Assert.AreEqual(
            "Выберите цель",
            result);
    }

    [Test]
    public void FromFailReason_ReturnsAvailable_ForNone()
    {
        SystemInteractionCtaState2A result =
            SystemInteractionCtaTextUtility2A
                .FromFailReason("None");

        Assert.AreEqual(
            SystemInteractionCtaState2A.Available,
            result);
    }

    [Test]
    public void FromFailReason_ReturnsNoTarget_ForNoTarget()
    {
        SystemInteractionCtaState2A result =
            SystemInteractionCtaTextUtility2A
                .FromFailReason("NoTarget");

        Assert.AreEqual(
            SystemInteractionCtaState2A.NoTarget,
            result);
    }

    [Test]
    public void FromFailReason_ReturnsOutOfRange_ForOutOfRange()
    {
        SystemInteractionCtaState2A result =
            SystemInteractionCtaTextUtility2A
                .FromFailReason("OutOfRange");

        Assert.AreEqual(
            SystemInteractionCtaState2A.OutOfRange,
            result);
    }

    [Test]
    public void FromFailReason_ReturnsUnavailable_ForUnknownReason()
    {
        SystemInteractionCtaState2A result =
            SystemInteractionCtaTextUtility2A
                .FromFailReason("SomeUnknownReason");

        Assert.AreEqual(
            SystemInteractionCtaState2A.Unavailable,
            result);
    }
}