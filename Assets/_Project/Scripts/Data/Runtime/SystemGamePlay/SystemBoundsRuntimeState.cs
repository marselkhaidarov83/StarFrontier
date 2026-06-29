using System;
using UnityEngine;

/// <summary>
/// Runtime-границы локальной сцены звёздной системы.
///
/// Это не save-state.
/// Границы создаются из конфигов текущей системы
/// или из временного ShipMovementConfig.
/// </summary>
[Serializable]
public sealed class SystemBoundsRuntimeState
{
    public bool IsInitialized { get; private set; }

    public bool IsEnabled { get; private set; }

    public Vector2 Center { get; private set; }

    public Vector2 HalfSize { get; private set; }

    public Vector2 Min =>
        Center - HalfSize;

    public Vector2 Max =>
        Center + HalfSize;

    public void SetBounds(
        Vector2 center,
        Vector2 halfSize,
        bool isEnabled)
    {
        Center = center;

        HalfSize =
            new Vector2(
                Mathf.Abs(halfSize.x),
                Mathf.Abs(halfSize.y));

        IsEnabled = isEnabled;
        IsInitialized = true;
    }

    public bool Contains(Vector2 position)
    {
        if (!IsInitialized || !IsEnabled)
            return true;

        Vector2 min = Min;
        Vector2 max = Max;

        return position.x >= min.x
            && position.x <= max.x
            && position.y >= min.y
            && position.y <= max.y;
    }

    public Vector2 ClampPosition(Vector2 position)
    {
        if (!IsInitialized || !IsEnabled)
            return position;

        Vector2 min = Min;
        Vector2 max = Max;

        return new Vector2(
            Mathf.Clamp(position.x, min.x, max.x),
            Mathf.Clamp(position.y, min.y, max.y));
    }

    public void ResetAll()
    {
        IsInitialized = false;
        IsEnabled = false;
        Center = Vector2.zero;
        HalfSize = Vector2.zero;
    }
}