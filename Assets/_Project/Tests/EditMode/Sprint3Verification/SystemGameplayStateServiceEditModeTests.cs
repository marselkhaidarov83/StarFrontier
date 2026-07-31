using NUnit.Framework;
using UnityEngine;

public sealed class SystemGameplayStateServiceEditModeTests
{
    [Test]
    public void Constructor_CreatesAllRuntimeStates()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        Assert.IsNotNull(service.State);
        Assert.IsNotNull(service.Control);
        Assert.IsNotNull(service.Movement);
        Assert.IsNotNull(service.Camera);
        Assert.IsNotNull(service.Targeting);
        Assert.IsNotNull(service.Interaction);
    }

    [Test]
    public void MarkInitialized_SetsStateFlag()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.MarkInitialized();

        Assert.IsTrue(service.State.IsInitialized);
    }

    [Test]
    public void ResetAll_ClearsInitializedFlag()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.MarkInitialized();
        service.ResetAll();

        Assert.IsFalse(service.State.IsInitialized);
    }

    [Test]
    public void Control_ResetFrameFlags_ClearsOnlyFrameEvents()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.Control.SetInteractPressed();
        service.Control.SetRecenterCameraPressed();

        service.ResetFrameFlags();

        Assert.IsFalse(
            service.Control.InteractPressedThisFrame);

        Assert.IsFalse(
            service.Control.RecenterCameraPressedThisFrame);

        Assert.IsTrue(
            service.Control.InteractHeld);
    }

    [Test]
    public void Movement_SetVelocity_UpdatesSpeedAndFacing()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.Movement.SetVelocity(
            new Vector2(0f, 3f),
            new Vector2(0f, 3f));

        Assert.IsTrue(service.Movement.IsMoving);
        Assert.AreEqual(3f, service.Movement.CurrentSpeed);
        Assert.AreEqual(Vector2.up, service.Movement.FacingDirection);
    }

    [Test]
    public void Targeting_SetTarget_SetsHasTarget()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.Targeting.SetTarget(
            "planet_01",
            SystemGameplayTargetType.Planet,
            new Vector2(1f, 2f),
            3f,
            true,
            true,
            true);

        Assert.IsTrue(service.Targeting.HasTarget);
        Assert.AreEqual("planet_01", service.Targeting.CurrentTargetId);
        Assert.AreEqual(
            SystemGameplayTargetType.Planet,
            service.Targeting.CurrentTargetType);
    }

    [Test]
    public void Targeting_ClearTarget_RemovesTarget()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.Targeting.SetTarget(
            "station_01",
            SystemGameplayTargetType.Station,
            Vector2.zero,
            1f,
            true,
            true,
            true);

        service.Targeting.ClearTarget();

        Assert.IsFalse(service.Targeting.HasTarget);
        Assert.AreEqual(string.Empty, service.Targeting.CurrentTargetId);
        Assert.AreEqual(
            SystemGameplayTargetType.None,
            service.Targeting.CurrentTargetType);
    }

    [Test]
    public void Interaction_MarkCompleted_SetsFrameFlag()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.Interaction.SetInteractionInProgress(
            true,
            0.5f);

        service.Interaction.MarkCompleted();

        Assert.IsTrue(
            service.Interaction.InteractionCompletedThisFrame);

        Assert.IsFalse(
            service.Interaction.IsInteractionInProgress);

        Assert.AreEqual(
            0f,
            service.Interaction.HoldProgressNormalized);
    }

    [Test]
    public void ResetAll_ClearsTargetAndInteraction()
    {
        SystemGameplayStateService service =
            new SystemGameplayStateService();

        service.Targeting.SetTarget(
            "planet_01",
            SystemGameplayTargetType.Planet,
            Vector2.zero,
            1f,
            true,
            true,
            true);

        service.Interaction.SetAvailableInteraction(
            "planet_01",
            SystemGameplayTargetType.Planet,
            true);

        service.ResetAll();

        Assert.IsFalse(service.Targeting.HasTarget);
        Assert.IsFalse(service.Interaction.CanInteract);
    }
}
