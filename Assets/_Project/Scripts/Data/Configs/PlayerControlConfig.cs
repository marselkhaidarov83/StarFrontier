using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerControlConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Player Control")]
public sealed class PlayerControlConfig : ScriptableObject
{
    [Header("Input")]
    [SerializeField]
    [Range(0.05f, 1f)]
    private float deadZone = 0.12f;

    [SerializeField]
    [Range(0.1f, 3f)]
    private float inputSmoothing = 0.35f;

    [SerializeField]
    private bool normalizeDiagonalInput = true;

    [Header("Mobile Joystick")]
    [SerializeField]
    private bool useFloatingJoystick = false;

    [SerializeField]
    [Min(32f)]
    private float joystickRadiusPixels = 140f;

    [SerializeField]
    [Min(32f)]
    private float joystickHandleRadiusPixels = 70f;

    [SerializeField]
    [Min(0f)]
    private float joystickReturnSpeed = 18f;

    [Header("Touch Zones")]
    [SerializeField]
    [Min(0f)]
    private float interactButtonHoldSeconds = 0.15f;

    [SerializeField]
    [Min(0f)]
    private float doubleTapMaxSeconds = 0.25f;

    public float DeadZone => deadZone;
    public float InputSmoothing => inputSmoothing;
    public bool NormalizeDiagonalInput => normalizeDiagonalInput;

    public bool UseFloatingJoystick => useFloatingJoystick;
    public float JoystickRadiusPixels => joystickRadiusPixels;
    public float JoystickHandleRadiusPixels => joystickHandleRadiusPixels;
    public float JoystickReturnSpeed => joystickReturnSpeed;

    public float InteractButtonHoldSeconds => interactButtonHoldSeconds;
    public float DoubleTapMaxSeconds => doubleTapMaxSeconds;
}