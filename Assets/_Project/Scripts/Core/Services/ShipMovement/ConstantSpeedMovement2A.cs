using System;
using UnityEngine;

/// <summary>
/// Расчёт движения с постоянной скоростью для этапа 2А.
///
/// Не реализует:
/// - разгон;
/// - торможение;
/// - поворот.
///
/// Эти функции перенесены на этап 3.
/// </summary>
public static class ConstantSpeedMovement2A
{
    private const float InputThresholdSqrMagnitude = 0.000001f;

    public static Vector2 Step(
        Vector2 currentPosition,
        Vector2 moveInput,
        float speedUnitsPerSecond,
        float deltaTime)
    {
        if (!IsFinite(currentPosition))
        {
            throw new ArgumentException(
                "Current position must contain finite values.",
                nameof(currentPosition));
        }

        if (!IsFinite(moveInput))
        {
            return currentPosition;
        }

        if (!IsFinite(speedUnitsPerSecond) ||
            speedUnitsPerSecond < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speedUnitsPerSecond),
                speedUnitsPerSecond,
                "Speed must be a finite non-negative value.");
        }

        if (!IsFinite(deltaTime) ||
            deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaTime),
                deltaTime,
                "Delta time must be a finite non-negative value.");
        }

        if (moveInput.sqrMagnitude <
            InputThresholdSqrMagnitude)
        {
            return currentPosition;
        }

        if (speedUnitsPerSecond <= 0f ||
            deltaTime <= 0f)
        {
            return currentPosition;
        }

        Vector2 direction = moveInput.normalized;

        return currentPosition +
               direction *
               speedUnitsPerSecond *
               deltaTime;
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}