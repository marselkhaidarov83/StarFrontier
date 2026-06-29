using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ShipMovementServiceEditModeTests
{
    [Test]
    public void Constructor_CreatesEnabledService()
    {
        ShipMovementService2A service =
            CreateService(out _);

        Assert.IsTrue(service.IsEnabled);
        Assert.IsNotNull(service.State);
        Assert.AreEqual(Vector2.zero, service.State.Position);
        Assert.AreEqual(Vector2.zero, service.State.Velocity);
        Assert.AreEqual(Vector2.up, service.State.FacingDirection);
    }

    [Test]
    public void Tick_WithMoveInput_AcceleratesAndMovesShip()
    {
        ShipMovementService2A service =
            CreateService(out SystemGameplayStateService stateService);

        stateService.Control.SetMoveInput(
            Vector2.up,
            Vector2.up,
            0f);

        service.Tick(0.5f);

        Assert.IsTrue(service.State.IsMoving);
        Assert.Greater(service.State.CurrentSpeed, 0f);
        Assert.Greater(service.State.Position.y, 0f);
    }

    [Test]
    public void Tick_DoesNotExceedMaxSpeed()
    {
        ShipMovementConfig config =
            CreateConfig(
                maxSpeed: 3f,
                acceleration: 100f,
                deceleration: 100f,
                turnSpeedDegrees: 360f,
                clampToBounds: false,
                bounds: new Vector2(100f, 100f));

        ShipMovementService2A service =
            CreateService(
                out SystemGameplayStateService stateService,
                config);

        stateService.Control.SetMoveInput(
            Vector2.right,
            Vector2.right,
            0f);

        service.Tick(1f);

        Assert.LessOrEqual(
            service.State.CurrentSpeed,
            3.0001f);
    }

    [Test]
    public void Tick_WithoutInput_DeceleratesShip()
    {
        ShipMovementConfig config =
            CreateConfig(
                maxSpeed: 10f,
                acceleration: 100f,
                deceleration: 5f,
                turnSpeedDegrees: 360f,
                clampToBounds: false,
                bounds: new Vector2(100f, 100f));

        ShipMovementService2A service =
            CreateService(
                out SystemGameplayStateService stateService,
                config);

        stateService.Control.SetMoveInput(
            Vector2.up,
            Vector2.up,
            0f);

        service.Tick(0.1f);

        float speedAfterAcceleration =
            service.State.CurrentSpeed;

        stateService.Control.SetMoveInput(
            Vector2.zero,
            Vector2.zero,
            0.1f);

        service.Tick(0.1f);

        Assert.Less(
            service.State.CurrentSpeed,
            speedAfterAcceleration);

        Assert.IsTrue(service.State.IsBraking);
    }

    [Test]
    public void Tick_RotatesTowardsMoveDirection()
    {
        ShipMovementConfig config =
            CreateConfig(
                maxSpeed: 10f,
                acceleration: 100f,
                deceleration: 100f,
                turnSpeedDegrees: 90f,
                clampToBounds: false,
                bounds: new Vector2(100f, 100f));

        ShipMovementService2A service =
            CreateService(
                out SystemGameplayStateService stateService,
                config);

        stateService.Control.SetMoveInput(
            Vector2.right,
            Vector2.right,
            0f);

        service.Tick(1f);

        Assert.That(
            service.State.FacingDirection.x,
            Is.EqualTo(1f).Within(0.0001f));

        Assert.That(
            service.State.FacingDirection.y,
            Is.EqualTo(0f).Within(0.0001f));

        Assert.That(
            service.State.RotationDegrees,
            Is.EqualTo(90f).Within(0.0001f));
    }

    [Test]
    public void Tick_WithBounds_ClampsPosition()
    {
        ShipMovementConfig config =
            CreateConfig(
                maxSpeed: 10f,
                acceleration: 100f,
                deceleration: 100f,
                turnSpeedDegrees: 360f,
                clampToBounds: true,
                bounds: new Vector2(0.5f, 0.5f));

        ShipMovementService2A service =
            CreateService(
                out SystemGameplayStateService stateService,
                config);

        stateService.Control.SetMoveInput(
            Vector2.right,
            Vector2.right,
            0f);

        service.Tick(1f);

        Assert.LessOrEqual(
            service.State.Position.x,
            0.5f);

        Assert.IsTrue(
            service.State.WasClampedToBounds);
    }

    [Test]
    public void SetShipStats_OverridesSpeedAccelerationAndTurnRate()
    {
        ShipMovementConfig config =
            CreateConfig(
                maxSpeed: 100f,
                acceleration: 100f,
                deceleration: 100f,
                turnSpeedDegrees: 360f,
                clampToBounds: false,
                bounds: new Vector2(100f, 100f));

        ShipMovementService2A service =
            CreateService(
                out SystemGameplayStateService stateService,
                config);

        ShipFinalStats stats =
            new ShipFinalStats(
                "ship_test",
                maxHull: 100,
                maxShield: 50,
                maxSpeed: 2f,
                acceleration: 100f,
                turnRate: 45f,
                cargoCapacity: 10,
                weaponSlotCount: 1,
                moduleSlotCount: 1,
                combatSprite: null);

        service.SetShipStats(stats);

        stateService.Control.SetMoveInput(
            Vector2.right,
            Vector2.right,
            0f);

        service.Tick(1f);

        Assert.LessOrEqual(
            service.State.CurrentSpeed,
            2.0001f);

        Assert.That(
            service.State.RotationDegrees,
            Is.EqualTo(45f).Within(0.0001f));
    }

    [Test]
    public void InitializeFromPlayerState_ReadsPositionAndDirection()
    {
        ShipMovementService2A service =
            CreateService(out _);

        PlayerState playerState =
            new PlayerState();

        playerState.SystemMapShipPosition =
            new Vector3(2f, 3f, -2f);

        playerState.SystemMapShipDirection =
            new Vector3(1f, 0f, 0f);

        service.InitializeFromPlayerState(playerState);

        Assert.AreEqual(
            new Vector2(2f, 3f),
            service.State.Position);

        Assert.AreEqual(
            Vector2.right,
            service.State.FacingDirection);
    }

    [Test]
    public void WriteToPlayerState_WritesPositionAndDirection()
    {
        ShipMovementService2A service =
            CreateService(out _);

        service.SetPosition(new Vector2(4f, 5f));
        service.SetFacingDirection(Vector2.right);

        PlayerState playerState =
            new PlayerState();

        playerState.SystemMapShipPosition =
            new Vector3(0f, 0f, -2f);

        service.WriteToPlayerState(playerState);

        Assert.AreEqual(
            new Vector3(4f, 5f, -2f),
            playerState.SystemMapShipPosition);

        Assert.AreEqual(
            new Vector3(1f, 0f, 0f),
            playerState.SystemMapShipDirection);
    }

    [Test]
    public void StopImmediately_ClearsVelocity()
    {
        ShipMovementService2A service =
            CreateService(out SystemGameplayStateService stateService);

        stateService.Control.SetMoveInput(
            Vector2.up,
            Vector2.up,
            0f);

        service.Tick(0.5f);
        service.StopImmediately();

        Assert.AreEqual(
            Vector2.zero,
            service.State.Velocity);

        Assert.AreEqual(
            0f,
            service.State.CurrentSpeed);

        Assert.IsFalse(service.State.IsBraking);
    }

    [Test]
    public void SetEnabledFalse_StopsShip()
    {
        ShipMovementService2A service =
            CreateService(out SystemGameplayStateService stateService);

        stateService.Control.SetMoveInput(
            Vector2.up,
            Vector2.up,
            0f);

        service.Tick(0.5f);

        service.SetEnabled(false);

        Assert.IsFalse(service.IsEnabled);
        Assert.AreEqual(Vector2.zero, service.State.Velocity);
        Assert.AreEqual(0f, service.State.CurrentSpeed);
    }

    [Test]
    public void Tick_WithInvalidDeltaTime_Throws()
    {
        ShipMovementService2A service =
            CreateService(out _);

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => service.Tick(-0.01f));

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => service.Tick(float.NaN));

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => service.Tick(float.PositiveInfinity));
    }

    private static ShipMovementService2A CreateService(
        out SystemGameplayStateService stateService,
        ShipMovementConfig config = null)
    {
        if (config == null)
        {
            config =
                CreateConfig(
                    maxSpeed: 10f,
                    acceleration: 20f,
                    deceleration: 30f,
                    turnSpeedDegrees: 360f,
                    clampToBounds: false,
                    bounds: new Vector2(100f, 100f));
        }

        stateService =
            new SystemGameplayStateService();

        return new ShipMovementService2A(
            config,
            stateService);
    }

    private static ShipMovementConfig CreateConfig(
        float maxSpeed,
        float acceleration,
        float deceleration,
        float turnSpeedDegrees,
        bool clampToBounds,
        Vector2 bounds)
    {
        ShipMovementConfig config =
            ScriptableObject.CreateInstance<ShipMovementConfig>();

        SetPrivateField(config, "maxSpeed", maxSpeed);
        SetPrivateField(config, "acceleration", acceleration);
        SetPrivateField(config, "deceleration", deceleration);
        SetPrivateField(config, "brakingDeceleration", deceleration);
        SetPrivateField(config, "turnSpeedDegrees", turnSpeedDegrees);
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