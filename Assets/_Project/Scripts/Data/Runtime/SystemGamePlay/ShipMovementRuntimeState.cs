using System;
using UnityEngine;

/// <summary>
/// Временное состояние движения корабля игрока
/// внутри локальной сцены звёздной системы.
/// </summary>
[Serializable]
public sealed class ShipMovementRuntimeState
{
    public Vector2 Position { get; private set; }
    public Vector2 PreviousPosition { get; private set; }

    public Vector2 Velocity { get; private set; }
    public Vector2 DesiredVelocity { get; private set; }

    public Vector2 FacingDirection { get; private set; } =
        Vector2.up;

    public float RotationDegrees { get; private set; }
    public float CurrentSpeed { get; private set; }

    public bool IsMoving { get; private set; }
    public bool IsBraking { get; private set; }
    public bool WasClampedToBounds { get; private set; }

    public float VisualTilt { get; private set; }
    public float VisualBank { get; private set; }

    public void SetPosition(Vector2 position)
    {
        PreviousPosition = Position;
        Position = position;
    }

    public void SetVelocity(
        Vector2 velocity,
        Vector2 desiredVelocity)
    {
        Velocity = velocity;
        DesiredVelocity = desiredVelocity;
        CurrentSpeed = velocity.magnitude;
        IsMoving = CurrentSpeed > 0.001f;

        if (velocity.sqrMagnitude > 0.0001f)
            FacingDirection = velocity.normalized;
    }

    public void SetRotation(float rotationDegrees)
    {
        RotationDegrees = rotationDegrees;
    }

    public void SetBraking(bool isBraking)
    {
        IsBraking = isBraking;
    }

    public void SetClampedToBounds(bool wasClamped)
    {
        WasClampedToBounds = wasClamped;
    }

    public void SetVisualMotion(
        float visualTilt,
        float visualBank)
    {
        VisualTilt = visualTilt;
        VisualBank = visualBank;
    }

    public void ResetAll()
    {
        Position = Vector2.zero;
        PreviousPosition = Vector2.zero;

        Velocity = Vector2.zero;
        DesiredVelocity = Vector2.zero;

        FacingDirection = Vector2.up;
        RotationDegrees = 0f;
        CurrentSpeed = 0f;

        IsMoving = false;
        IsBraking = false;
        WasClampedToBounds = false;

        VisualTilt = 0f;
        VisualBank = 0f;
    }
}