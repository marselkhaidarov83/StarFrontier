using UnityEngine;

// Конфиг PlayerControlConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "PlayerControlConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Player Control")]
public sealed class PlayerControlConfig : BaseConfig
{
    [Header("Input")]
    [SerializeField]
    [Range(0.05f, 1f)]
    [Tooltip("Параметр deadZone. Используется связанными игровыми системами этого конфига.")]
    private float deadZone = 0.12f;

    [SerializeField]
    [Range(0.1f, 3f)]
    [Tooltip("Параметр inputSmoothing. Используется связанными игровыми системами этого конфига.")]
    private float inputSmoothing = 0.35f;

    [SerializeField]
    [Tooltip("Переключатель normalizeDiagonalInput. Включает или выключает соответствующее правило или отображение.")]
    private bool normalizeDiagonalInput = true;

    [Header("Mobile Joystick")]
    [SerializeField]
    [Tooltip("Переключатель useFloatingJoystick. Включает или выключает соответствующее правило или отображение.")]
    private bool useFloatingJoystick = false;

    [SerializeField]
    [Min(32f)]
    [Tooltip("Радиус для параметра joystickRadiusPixels. Используется при расчёте расстояний и зон действия.")]
    private float joystickRadiusPixels = 140f;

    [SerializeField]
    [Min(32f)]
    [Tooltip("Радиус для параметра joystickHandleRadiusPixels. Используется при расчёте расстояний и зон действия.")]
    private float joystickHandleRadiusPixels = 70f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Скорость для параметра joystickReturnSpeed. Используется при движении или анимации.")]
    private float joystickReturnSpeed = 18f;

    [Header("Touch Zones")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр interactButtonHoldSeconds. Используется связанными игровыми системами этого конфига.")]
    private float interactButtonHoldSeconds = 0.15f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Максимальное значение параметра doubleTapMaxSeconds. Используется как верхняя граница диапазона.")]
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
