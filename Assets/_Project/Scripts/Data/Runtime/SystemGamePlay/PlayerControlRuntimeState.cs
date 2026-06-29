using System;
using UnityEngine;

/// <summary>
/// Временное состояние управления игрока.
///
/// Здесь хранится ввод текущего кадра и обработанный ввод.
/// Состояние не сохраняется в save-файл.
/// </summary>
[Serializable]
public sealed class PlayerControlRuntimeState
{
    public Vector2 RawMoveInput { get; private set; }
    public Vector2 SmoothedMoveInput { get; private set; }

    public bool HasMoveInput { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool InteractHeld { get; private set; }
    public bool InteractReleasedThisFrame { get; private set; }

    public bool RecenterCameraPressedThisFrame { get; private set; }

    public float LastInputTimeSeconds { get; private set; }

    public void SetMoveInput(
        Vector2 rawInput,
        Vector2 smoothedInput,
        float timeSeconds)
    {
        RawMoveInput = rawInput;
        SmoothedMoveInput = smoothedInput;
        HasMoveInput = smoothedInput.sqrMagnitude > 0.0001f;
        LastInputTimeSeconds = timeSeconds;
    }

    public void SetInteractPressed()
    {
        InteractPressedThisFrame = true;
        InteractHeld = true;
    }

    public void SetInteractReleased()
    {
        InteractReleasedThisFrame = true;
        InteractHeld = false;
    }

    public void SetRecenterCameraPressed()
    {
        RecenterCameraPressedThisFrame = true;
    }

    /// <summary>
    /// Сбрасывает только события одного кадра.
    /// Удерживаемые значения остаются.
    /// </summary>
    public void ResetFrameInput()
    {
        InteractPressedThisFrame = false;
        InteractReleasedThisFrame = false;
        RecenterCameraPressedThisFrame = false;
    }

    /// <summary>
    /// Полный сброс состояния управления.
    /// </summary>
    public void ResetAll()
    {
        RawMoveInput = Vector2.zero;
        SmoothedMoveInput = Vector2.zero;
        HasMoveInput = false;

        InteractPressedThisFrame = false;
        InteractHeld = false;
        InteractReleasedThisFrame = false;

        RecenterCameraPressedThisFrame = false;
        LastInputTimeSeconds = 0f;
    }
}