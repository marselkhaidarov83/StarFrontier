using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SystemBoundsAndSaveSyncEditModeTests
{
    [Test]
    public void SystemBoundsRuntimeState_ClampsPosition()
    {
        SystemBoundsRuntimeState bounds =
            new SystemBoundsRuntimeState();

        bounds.SetBounds(
            Vector2.zero,
            new Vector2(2f, 3f),
            true);

        Vector2 clamped =
            bounds.ClampPosition(
                new Vector2(5f, -10f));

        Assert.AreEqual(
            new Vector2(2f, -3f),
            clamped);
    }

    [Test]
    public void SystemBoundsRuntimeState_DisabledBounds_DoNotClamp()
    {
        SystemBoundsRuntimeState bounds =
            new SystemBoundsRuntimeState();

        bounds.SetBounds(
            Vector2.zero,
            new Vector2(2f, 3f),
            false);

        Vector2 position =
            new Vector2(5f, -10f);

        Assert.AreEqual(
            position,
            bounds.ClampPosition(position));
    }

    [Test]
    public void SystemBoundsService_InitializesBoundsFromConfig()
    {
        ShipMovementConfig config =
            CreateMovementConfig(
                clampToBounds: true,
                bounds: new Vector2(4f, 6f));

        SystemGameplayStateService stateService =
            new SystemGameplayStateService();

        SystemBoundsService2A service =
            new SystemBoundsService2A(
                config,
                stateService);

        service.InitializeFromConfig();

        Assert.IsTrue(stateService.Bounds.IsInitialized);
        Assert.IsTrue(stateService.Bounds.IsEnabled);
        Assert.AreEqual(
            new Vector2(4f, 6f),
            stateService.Bounds.HalfSize);
    }

    [Test]
    public void ShipMovement_UsesRuntimeBounds()
    {
        ShipMovementConfig config =
            CreateMovementConfig(
                clampToBounds: false,
                bounds: new Vector2(100f, 100f));

        SystemGameplayStateService stateService =
            new SystemGameplayStateService();

        stateService.Bounds.SetBounds(
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            true);

        ShipMovementService2A movementService =
            new ShipMovementService2A(
                config,
                stateService);

        stateService.Control.SetMoveInput(
            Vector2.right,
            Vector2.right,
            0f);

        movementService.Tick(1f);

        Assert.LessOrEqual(
            movementService.State.Position.x,
            0.5f);

        Assert.IsTrue(
            movementService.State.WasClampedToBounds);
    }

    [Test]
    public void SaveSync_WriteMovementToSave_WritesPositionAndDirection()
    {
        ShipMovementService2A movementService =
            CreateMovementService();

        movementService.SetPosition(
            new Vector2(7f, 8f));

        movementService.SetFacingDirection(
            Vector2.right);

        PlayerShipSaveSyncService2A syncService =
            new PlayerShipSaveSyncService2A(
                movementService);

        GameRuntimeState state =
            new GameRuntimeState();

        state.Player.SystemMapShipPosition =
            new Vector3(0f, 0f, -2f);

        syncService.WriteMovementToSave(state);

        Assert.AreEqual(
            new Vector3(7f, 8f, -2f),
            state.Player.SystemMapShipPosition);

        Assert.AreEqual(
            new Vector3(1f, 0f, 0f),
            state.Player.SystemMapShipDirection);
    }

    [Test]
    public void SaveSync_InitializeMovementFromSave_ReadsPositionAndDirection()
    {
        ShipMovementService2A movementService =
            CreateMovementService();

        PlayerShipSaveSyncService2A syncService =
            new PlayerShipSaveSyncService2A(
                movementService);

        GameRuntimeState state =
            new GameRuntimeState();

        state.Player.SystemMapShipPosition =
            new Vector3(3f, 4f, -2f);

        state.Player.SystemMapShipDirection =
            new Vector3(0f, 1f, 0f);

        syncService.InitializeMovementFromSave(state);

        Assert.AreEqual(
            new Vector2(3f, 4f),
            movementService.State.Position);

        Assert.AreEqual(
            Vector2.up,
            movementService.State.FacingDirection);
    }

    private static ShipMovementService2A CreateMovementService()
    {
        ShipMovementConfig config =
            CreateMovementConfig(
                clampToBounds: false,
                bounds: new Vector2(100f, 100f));

        SystemGameplayStateService stateService =
            new SystemGameplayStateService();

        return new ShipMovementService2A(
            config,
            stateService);
    }

    private static ShipMovementConfig CreateMovementConfig(
        bool clampToBounds,
        Vector2 bounds)
    {
        ShipMovementConfig config =
            ScriptableObject.CreateInstance<ShipMovementConfig>();

        SetPrivateField(config, "maxSpeed", 10f);
        SetPrivateField(config, "acceleration", 100f);
        SetPrivateField(config, "deceleration", 100f);
        SetPrivateField(config, "brakingDeceleration", 100f);
        SetPrivateField(config, "turnSpeedDegrees", 360f);
        SetPrivateField(config, "rotationSmoothing", 0f);
        SetPrivateField(config, "visualTiltAmount", 0.18f);
        SetPrivateField(config, "visualBankAmount", 0.25f);
        SetPrivateField(config, "visualTiltReturnSpeed", 8f);
        SetPrivateField(config, "clampToSystemBounds", clampToBounds);
        SetPrivateField(config, "systemBoundsHalfSize", bounds);

        return config;
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field =
            FindField(
                target.GetType(),
                fieldName);

        Assert.IsNotNull(
            field,
            $"Field '{fieldName}' was not found on {target.GetType().Name}.");

        field.SetValue(target, value);
    }

    private static FieldInfo FindField(
        System.Type type,
        string fieldName)
    {
        while (type != null)
        {
            FieldInfo field =
                type.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);

            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }
}
