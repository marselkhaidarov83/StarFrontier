using System;
using UnityEngine;

/// <summary>
/// Обрабатывает сырой ввод игрока и записывает результат
/// в PlayerControlRuntimeState.
///
/// Сервис не читает InputActions напрямую.
/// Ввод передаётся извне через методы SetRawMoveInput,
/// PressInteract, ReleaseInteract и PressRecenterCamera.
/// </summary>
public sealed class PlayerControlService2A : IPlayerControlService
{
    private const float MinSmoothing = 0.0001f;
    private const float StopThresholdSqrMagnitude = 0.000001f;

    private readonly PlayerControlConfig _config;
    private readonly ISystemGameplayStateService _stateService;

    private Vector2 _rawMoveInput;
    private Vector2 _processedMoveInput;

    private bool _interactPressedQueued;
    private bool _interactReleasedQueued;
    private bool _recenterCameraPressedQueued;

    private float _localTimeSeconds;

    public PlayerControlService2A()
    {
        IConfigService configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _config = configService.PlayerControlConfig;
        _stateService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemGameplayStateService>();

        IsEnabled = true;
    }

    public PlayerControlService2A(PlayerControlConfig playerControlConfig,
        ISystemGameplayStateService systemGameplayStateService)
    {
        _config = playerControlConfig;
        _stateService = systemGameplayStateService;

        IsEnabled = true;
    }

    public bool IsEnabled { get; private set; }

    public Vector2 RawMoveInput => _rawMoveInput;

    public Vector2 ProcessedMoveInput => _processedMoveInput;

    public void SetEnabled(bool isEnabled)
    {
        if (IsEnabled == isEnabled)
            return;

        IsEnabled = isEnabled;

        if (!IsEnabled)
            ResetAll();
    }

    public void SetRawMoveInput(Vector2 rawMoveInput)
    {
        if (!IsFinite(rawMoveInput))
        {
            _rawMoveInput = Vector2.zero;
            return;
        }

        _rawMoveInput = Vector2.ClampMagnitude(
            rawMoveInput,
            1f);
    }

    public void PressInteract()
    {
        _interactPressedQueued = true;
    }

    public void ReleaseInteract()
    {
        _interactReleasedQueued = true;
    }

    public void PressRecenterCamera()
    {
        _recenterCameraPressedQueued = true;
    }

    public void Tick(float deltaTime)
    {
        ValidateDeltaTime(deltaTime);

        _localTimeSeconds += deltaTime;

        PlayerControlRuntimeState controlState =
            _stateService.Control;

        /*
         * Сбрасываем только одноразовые флаги кадра.
         * Удержание кнопки взаимодействия при этом сохраняется.
         */
        controlState.ResetFrameInput();

        if (!IsEnabled)
        {
            _rawMoveInput = Vector2.zero;
            _processedMoveInput = Vector2.zero;

            controlState.SetMoveInput(
                Vector2.zero,
                Vector2.zero,
                _localTimeSeconds);

            ClearQueuedFrameEvents();
            return;
        }

        Vector2 targetMoveInput =
            ApplyDeadZone(_rawMoveInput, _config.DeadZone);

        if (_config.NormalizeDiagonalInput)
        {
            targetMoveInput =
                Vector2.ClampMagnitude(targetMoveInput, 1f);
        }

        _processedMoveInput =
            SmoothInput(
                _processedMoveInput,
                targetMoveInput,
                deltaTime,
                _config.InputSmoothing);

        if (_processedMoveInput.sqrMagnitude <
            StopThresholdSqrMagnitude)
        {
            _processedMoveInput = Vector2.zero;
        }

        controlState.SetMoveInput(
            _rawMoveInput,
            _processedMoveInput,
            _localTimeSeconds);

        if (_interactPressedQueued)
            controlState.SetInteractPressed();

        if (_interactReleasedQueued)
            controlState.SetInteractReleased();

        if (_recenterCameraPressedQueued)
            controlState.SetRecenterCameraPressed();

        ClearQueuedFrameEvents();
    }

    public void ResetAll()
    {
        _rawMoveInput = Vector2.zero;
        _processedMoveInput = Vector2.zero;

        _interactPressedQueued = false;
        _interactReleasedQueued = false;
        _recenterCameraPressedQueued = false;

        _stateService.Control.ResetAll();
    }

    private void ClearQueuedFrameEvents()
    {
        _interactPressedQueued = false;
        _interactReleasedQueued = false;
        _recenterCameraPressedQueued = false;
    }

    private static Vector2 ApplyDeadZone(
        Vector2 input,
        float deadZone)
    {
        float clampedDeadZone =
            Mathf.Clamp01(deadZone);

        if (input.magnitude <= clampedDeadZone)
            return Vector2.zero;

        /*
         * Перенормировка делает управление плавнее:
         * сразу после dead zone значение начинается с 0,
         * а не прыгает на deadZone.
         */
        float magnitude =
            input.magnitude;

        float normalizedMagnitude =
            Mathf.InverseLerp(
                clampedDeadZone,
                1f,
                magnitude);

        return input.normalized * normalizedMagnitude;
    }

    private static Vector2 SmoothInput(
        Vector2 current,
        Vector2 target,
        float deltaTime,
        float smoothingSeconds)
    {
        if (smoothingSeconds <= MinSmoothing)
            return target;

        float t =
            1f - Mathf.Exp(-deltaTime / smoothingSeconds);

        return Vector2.Lerp(
            current,
            target,
            t);
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