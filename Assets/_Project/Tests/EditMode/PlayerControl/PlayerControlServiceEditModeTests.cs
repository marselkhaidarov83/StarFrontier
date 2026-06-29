using NUnit.Framework;
using UnityEngine;

public sealed class PlayerControlServiceEditModeTests
{
    [Test]
    public void Constructor_CreatesEnabledService()
    {
        PlayerControlService2A service =
            CreateService(out _);

        Assert.IsTrue(service.IsEnabled);
        Assert.AreEqual(Vector2.zero, service.RawMoveInput);
        Assert.AreEqual(Vector2.zero, service.ProcessedMoveInput);
    }

    [Test]
    public void SetRawMoveInput_StoresClampedRawInput()
    {
        PlayerControlService2A service =
            CreateService(out _);

        service.SetRawMoveInput(new Vector2(10f, 0f));

        Assert.AreEqual(
            Vector2.right,
            service.RawMoveInput);
    }

    [Test]
    public void Tick_WithInputBelowDeadZone_WritesZeroMoveInput()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.SetRawMoveInput(new Vector2(0.01f, 0f));
        service.Tick(0.1f);

        Assert.IsFalse(stateService.Control.HasMoveInput);
        Assert.AreEqual(
            Vector2.zero,
            stateService.Control.SmoothedMoveInput);
    }

    [Test]
    public void Tick_WithMoveInput_WritesSmoothedMoveInput()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.SetRawMoveInput(Vector2.up);
        service.Tick(1f);

        Assert.IsTrue(stateService.Control.HasMoveInput);
        Assert.Greater(
            stateService.Control.SmoothedMoveInput.y,
            0.5f);
    }

    [Test]
    public void Tick_NormalizesDiagonalInput()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.SetRawMoveInput(new Vector2(1f, 1f));
        service.Tick(10f);

        Assert.LessOrEqual(
            stateService.Control.SmoothedMoveInput.magnitude,
            1.01f);
    }

    [Test]
    public void PressInteract_SetsPressedAndHeldForOneFrame()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.PressInteract();
        service.Tick(0.1f);

        Assert.IsTrue(
            stateService.Control.InteractPressedThisFrame);

        Assert.IsTrue(
            stateService.Control.InteractHeld);

        service.Tick(0.1f);

        Assert.IsFalse(
            stateService.Control.InteractPressedThisFrame);

        Assert.IsTrue(
            stateService.Control.InteractHeld);
    }

    [Test]
    public void ReleaseInteract_SetsReleasedAndClearsHeld()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.PressInteract();
        service.Tick(0.1f);

        service.ReleaseInteract();
        service.Tick(0.1f);

        Assert.IsTrue(
            stateService.Control.InteractReleasedThisFrame);

        Assert.IsFalse(
            stateService.Control.InteractHeld);

        service.Tick(0.1f);

        Assert.IsFalse(
            stateService.Control.InteractReleasedThisFrame);
    }

    [Test]
    public void PressRecenterCamera_SetsFrameFlag()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.PressRecenterCamera();
        service.Tick(0.1f);

        Assert.IsTrue(
            stateService.Control.RecenterCameraPressedThisFrame);

        service.Tick(0.1f);

        Assert.IsFalse(
            stateService.Control.RecenterCameraPressedThisFrame);
    }

    [Test]
    public void SetEnabledFalse_ResetsStateAndIgnoresInput()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.SetRawMoveInput(Vector2.up);
        service.PressInteract();

        service.SetEnabled(false);
        service.Tick(0.1f);

        Assert.IsFalse(service.IsEnabled);
        Assert.AreEqual(Vector2.zero, service.RawMoveInput);
        Assert.AreEqual(Vector2.zero, service.ProcessedMoveInput);

        Assert.IsFalse(stateService.Control.HasMoveInput);
        Assert.IsFalse(stateService.Control.InteractHeld);
    }

    [Test]
    public void ResetAll_ClearsControlState()
    {
        PlayerControlService2A service =
            CreateService(out SystemGameplayStateService stateService);

        service.SetRawMoveInput(Vector2.up);
        service.PressInteract();
        service.Tick(0.1f);

        service.ResetAll();

        Assert.AreEqual(Vector2.zero, service.RawMoveInput);
        Assert.AreEqual(Vector2.zero, service.ProcessedMoveInput);

        Assert.IsFalse(stateService.Control.HasMoveInput);
        Assert.IsFalse(stateService.Control.InteractHeld);
        Assert.IsFalse(stateService.Control.InteractPressedThisFrame);
    }

    [Test]
    public void Tick_WithInvalidDeltaTime_Throws()
    {
        PlayerControlService2A service =
            CreateService(out _);

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => service.Tick(-0.01f));

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => service.Tick(float.NaN));

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => service.Tick(float.PositiveInfinity));
    }

    private static PlayerControlService2A CreateService(
        out SystemGameplayStateService stateService)
    {
        PlayerControlConfig config =
            ScriptableObject.CreateInstance<PlayerControlConfig>();

        stateService =
            new SystemGameplayStateService();

        return new PlayerControlService2A(
            config,
            stateService);
    }
}
