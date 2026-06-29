using System;
using UnityEngine;

/// <summary>
/// Рассчитывает ускорение, торможение, скорость,
/// позицию и поворот корабля игрока.
///
/// Сервис читает обработанный ввод из PlayerControlRuntimeState
/// и пишет результат в ShipMovementRuntimeState.
/// </summary>
public sealed class ShipMovementService2A : IShipMovementService
{
    private const float InputThresholdSqrMagnitude = 0.0001f;
    private const float VelocityThresholdSqrMagnitude = 0.000001f;

    private readonly ShipMovementConfig _shipMovementConfig;
    private readonly ISystemGameplayStateService _stateService;

    private ShipFinalStats _shipStats;

    public ShipMovementService2A()
    {
        IConfigService configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _shipMovementConfig = configService.ShipMovementConfig;
        _stateService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemGameplayStateService>();

        IsEnabled = true;
    }

    public ShipMovementService2A(
        ShipMovementConfig shipMovementConfig,
        ISystemGameplayStateService systemGameplayStateService)
    {
        if (shipMovementConfig == null)
        {
            throw new ArgumentNullException(
                nameof(shipMovementConfig));
        }

        if (systemGameplayStateService == null)
        {
            throw new ArgumentNullException(
                nameof(systemGameplayStateService));
        }

        _shipMovementConfig = shipMovementConfig;
        _stateService = systemGameplayStateService;

        IsEnabled = true;
    }

    public bool IsEnabled { get; private set; }

    public ShipMovementRuntimeState State =>
        _stateService.Movement;

    public void SetEnabled(bool isEnabled)
    {
        if (IsEnabled == isEnabled)
            return;

        IsEnabled = isEnabled;

        if (!IsEnabled)
            StopImmediately();
    }

    public void SetShipStats(ShipFinalStats shipStats)
    {
        _shipStats = shipStats;
    }

    public void ClearShipStats()
    {
        _shipStats = null;
    }

    public void SetPosition(Vector2 position)
    {
        State.SetPosition(position);
    }

    public void SetFacingDirection(Vector2 direction)
    {
        if (!IsFinite(direction)
            || direction.sqrMagnitude < InputThresholdSqrMagnitude)
        {
            State.SetFacingDirection(Vector2.up);
            return;
        }

        State.SetFacingDirection(direction.normalized);
    }

    public void InitializeFromPlayerState(PlayerState playerState)
    {
        if (playerState == null)
            return;

        Vector3 savedPosition =
            playerState.SystemMapShipPosition;

        Vector3 savedDirection =
            playerState.SystemMapShipDirection;

        State.SetPosition(
            new Vector2(
                savedPosition.x,
                savedPosition.y));

        Vector2 direction =
            new Vector2(
                savedDirection.x,
                savedDirection.y);

        if (!IsFinite(direction)
            || direction.sqrMagnitude < InputThresholdSqrMagnitude)
        {
            direction = Vector2.up;
        }

        State.SetFacingDirection(direction.normalized);
    }

    public void WriteToPlayerState(PlayerState playerState)
    {
        if (playerState == null)
            return;

        Vector3 previousPosition =
            playerState.SystemMapShipPosition;

        playerState.SystemMapShipPosition =
            new Vector3(
                State.Position.x,
                State.Position.y,
                previousPosition.z);

        playerState.SystemMapShipDirection =
            new Vector3(
                State.FacingDirection.x,
                State.FacingDirection.y,
                0f);
    }

    public void Tick(float deltaTime)
    {
        ValidateDeltaTime(deltaTime);

        if (!IsEnabled)
            return;

        PlayerControlRuntimeState controlState =
            _stateService.Control;

        ShipMovementRuntimeState movementState =
            _stateService.Movement;

        Vector2 moveInput =
            controlState.SmoothedMoveInput;

        if (!IsFinite(moveInput))
            moveInput = Vector2.zero;

        moveInput =
            Vector2.ClampMagnitude(moveInput, 1f);

        bool hasMoveInput =
            moveInput.sqrMagnitude >= InputThresholdSqrMagnitude;

        float maxSpeed =
            GetMaxSpeed();

        float acceleration =
            GetAcceleration();

        float deceleration =
            hasMoveInput
                ? acceleration
                : GetDeceleration();

        Vector2 desiredVelocity =
            hasMoveInput
                ? moveInput * maxSpeed
                : Vector2.zero;

        Vector2 newVelocity =
            Vector2.MoveTowards(
                movementState.Velocity,
                desiredVelocity,
                deceleration * deltaTime);

        if (newVelocity.sqrMagnitude <
            VelocityThresholdSqrMagnitude)
        {
            newVelocity = Vector2.zero;
        }

        Vector2 newPosition =
            movementState.Position
            + newVelocity * deltaTime;

        bool wasClamped =
            ClampPositionAndVelocity(
                ref newPosition,
                ref newVelocity);

        Vector2 targetFacingDirection =
            GetTargetFacingDirection(
                movementState,
                moveInput,
                newVelocity,
                hasMoveInput);

        Vector2 newFacingDirection =
            RotateTowards(
                movementState.FacingDirection,
                targetFacingDirection,
                GetTurnRate() * deltaTime);

        movementState.SetPosition(newPosition);

        movementState.SetVelocity(
            newVelocity,
            desiredVelocity);

        movementState.SetFacingDirection(
            newFacingDirection);

        movementState.SetBraking(
            !hasMoveInput
            && movementState.CurrentSpeed > 0.001f);

        movementState.SetClampedToBounds(wasClamped);

        ApplyVisualMotion(
            movementState,
            moveInput,
            newVelocity,
            maxSpeed);
    }

    public void StopImmediately()
    {
        State.SetVelocity(
            Vector2.zero,
            Vector2.zero);

        State.SetBraking(false);
        State.SetClampedToBounds(false);
        State.SetVisualMotion(0f, 0f);
    }

    public void ResetAll()
    {
        ClearShipStats();
        State.ResetAll();
        IsEnabled = true;
    }

    private float GetMaxSpeed()
    {
        if (_shipStats != null)
            return Mathf.Max(0f, _shipStats.MaxSpeed);

        return Mathf.Max(0f, _shipMovementConfig.MaxSpeed);
    }

    private float GetAcceleration()
    {
        if (_shipStats != null)
            return Mathf.Max(0f, _shipStats.Acceleration);

        return Mathf.Max(0f, _shipMovementConfig.Acceleration);
    }

    private float GetTurnRate()
    {
        if (_shipStats != null)
            return Mathf.Max(0f, _shipStats.TurnRate);

        return Mathf.Max(0f, _shipMovementConfig.TurnSpeedDegrees);
    }

    private float GetDeceleration()
    {
        return Mathf.Max(0f, _shipMovementConfig.Deceleration);
    }

    private bool ClampPositionAndVelocity(
    ref Vector2 position,
    ref Vector2 velocity)
    {
        SystemBoundsRuntimeState boundsState =
            _stateService.Bounds;

        if (boundsState != null
            && boundsState.IsInitialized)
        {
            return ClampByRuntimeBounds(
                boundsState,
                ref position,
                ref velocity);
        }

        return ClampByConfigBounds(
            ref position,
            ref velocity);
    }

    private static bool ClampByRuntimeBounds(
        SystemBoundsRuntimeState boundsState,
        ref Vector2 position,
        ref Vector2 velocity)
    {
        if (!boundsState.IsEnabled)
            return false;

        Vector2 originalPosition =
            position;

        position =
            boundsState.ClampPosition(position);

        bool wasClamped =
            originalPosition != position;

        if (!wasClamped)
            return false;

        StopVelocityOnClampedAxes(
            originalPosition,
            position,
            ref velocity);

        return true;
    }

    private bool ClampByConfigBounds(
        ref Vector2 position,
        ref Vector2 velocity)
    {
        if (!_shipMovementConfig.ClampToSystemBounds)
            return false;

        Vector2 halfSize =
            _shipMovementConfig.SystemBoundsHalfSize;

        Vector2 min =
            new Vector2(
                -Mathf.Abs(halfSize.x),
                -Mathf.Abs(halfSize.y));

        Vector2 max =
            new Vector2(
                Mathf.Abs(halfSize.x),
                Mathf.Abs(halfSize.y));

        Vector2 originalPosition =
            position;

        position =
            new Vector2(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y));

        bool wasClamped =
            originalPosition != position;

        if (!wasClamped)
            return false;

        StopVelocityOnClampedAxes(
            originalPosition,
            position,
            ref velocity);

        return true;
    }

    private static void StopVelocityOnClampedAxes(
        Vector2 originalPosition,
        Vector2 clampedPosition,
        ref Vector2 velocity)
    {
        if (!Mathf.Approximately(
                originalPosition.x,
                clampedPosition.x))
        {
            velocity.x = 0f;
        }

        if (!Mathf.Approximately(
                originalPosition.y,
                clampedPosition.y))
        {
            velocity.y = 0f;
        }
    }


    private static Vector2 GetTargetFacingDirection(
        ShipMovementRuntimeState movementState,
        Vector2 moveInput,
        Vector2 velocity,
        bool hasMoveInput)
    {
        if (hasMoveInput)
            return moveInput.normalized;

        if (velocity.sqrMagnitude >=
            InputThresholdSqrMagnitude)
        {
            return velocity.normalized;
        }

        if (movementState.FacingDirection.sqrMagnitude >=
            InputThresholdSqrMagnitude)
        {
            return movementState.FacingDirection.normalized;
        }

        return Vector2.up;
    }

    private void ApplyVisualMotion(
        ShipMovementRuntimeState movementState,
        Vector2 moveInput,
        Vector2 velocity,
        float maxSpeed)
    {
        float speedNormalized =
            maxSpeed <= 0f
                ? 0f
                : Mathf.Clamp01(
                    velocity.magnitude / maxSpeed);

        float visualTilt =
            speedNormalized * _shipMovementConfig.VisualTiltAmount;

        float visualBank =
            -moveInput.x * _shipMovementConfig.VisualBankAmount;

        movementState.SetVisualMotion(
            visualTilt,
            visualBank);
    }

    private static Vector2 RotateTowards(
    Vector2 currentDirection,
    Vector2 targetDirection,
    float maxDegreesDelta)
    {
        if (!IsFinite(currentDirection)
            || currentDirection.sqrMagnitude <
            InputThresholdSqrMagnitude)
        {
            currentDirection = Vector2.up;
        }

        if (!IsFinite(targetDirection)
            || targetDirection.sqrMagnitude <
            InputThresholdSqrMagnitude)
        {
            targetDirection = currentDirection;
        }

        float currentAngle =
            AngleFromUpClockwise(
                currentDirection.normalized);

        float targetAngle =
            AngleFromUpClockwise(
                targetDirection.normalized);

        float newAngle =
            Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                Mathf.Max(0f, maxDegreesDelta));

        return DirectionFromUpClockwiseAngle(newAngle);
    }

    private static float AngleFromUpClockwise(
        Vector2 direction)
    {
        return -Vector2.SignedAngle(
            Vector2.up,
            direction);
    }

    private static Vector2 DirectionFromUpClockwiseAngle(
        float degrees)
    {
        float radians =
            degrees * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Sin(radians),
            Mathf.Cos(radians)).normalized;
    }

    private static void ValidateDeltaTime(float deltaTime)
    {
        if (float.IsNaN(deltaTime)
            || float.IsInfinity(deltaTime)
            || deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaTime),
                deltaTime,
                "Delta time must be a finite non-negative value.");
        }
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value);
    }
}