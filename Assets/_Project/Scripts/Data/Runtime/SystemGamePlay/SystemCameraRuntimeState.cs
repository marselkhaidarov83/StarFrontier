using System;
using UnityEngine;

/// <summary>
/// Временное состояние камеры локальной системы.
/// </summary>
[Serializable]
public sealed class SystemCameraRuntimeState
{
    public Vector3 Position { get; private set; } =
        new Vector3(0f, 0f, -10f);

    public Vector3 TargetPosition { get; private set; }
    public Vector2 LookAheadOffset { get; private set; }
    public Vector2 ShakeOffset { get; private set; }

    public float OrthographicSize { get; private set; }
    public bool IsFollowingPlayer { get; private set; } = true;
    public bool IsClampedToBounds { get; private set; }

    public float ShakeRemainingSeconds { get; private set; }
    public float ShakeAmplitude { get; private set; }

    public void SetPosition(Vector3 position)
    {
        Position = position;
    }

    public void SetTargetPosition(Vector3 targetPosition)
    {
        TargetPosition = targetPosition;
    }

    public void SetOrthographicSize(float orthographicSize)
    {
        OrthographicSize = Mathf.Max(0.01f, orthographicSize);
    }

    public void SetLookAheadOffset(Vector2 lookAheadOffset)
    {
        LookAheadOffset = lookAheadOffset;
    }

    public void SetFollowingPlayer(bool isFollowingPlayer)
    {
        IsFollowingPlayer = isFollowingPlayer;
    }

    public void SetClampedToBounds(bool isClamped)
    {
        IsClampedToBounds = isClamped;
    }

    public void StartShake(
        float durationSeconds,
        float amplitude)
    {
        ShakeRemainingSeconds = Mathf.Max(0f, durationSeconds);
        ShakeAmplitude = Mathf.Max(0f, amplitude);
    }

    public void SetShakeOffset(Vector2 shakeOffset)
    {
        ShakeOffset = shakeOffset;
    }

    public void TickShake(float deltaTime)
    {
        if (ShakeRemainingSeconds <= 0f)
        {
            ShakeRemainingSeconds = 0f;
            ShakeAmplitude = 0f;
            ShakeOffset = Vector2.zero;
            return;
        }

        ShakeRemainingSeconds =
            Mathf.Max(0f, ShakeRemainingSeconds - deltaTime);

        if (ShakeRemainingSeconds <= 0f)
        {
            ShakeAmplitude = 0f;
            ShakeOffset = Vector2.zero;
        }
    }

    public void ResetAll()
    {
        Position = new Vector3(0f, 0f, -10f);
        TargetPosition = Vector3.zero;
        LookAheadOffset = Vector2.zero;
        ShakeOffset = Vector2.zero;

        OrthographicSize = 0f;
        IsFollowingPlayer = true;
        IsClampedToBounds = false;

        ShakeRemainingSeconds = 0f;
        ShakeAmplitude = 0f;
    }
}