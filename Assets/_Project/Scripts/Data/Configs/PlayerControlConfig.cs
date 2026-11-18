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

    public float DeadZone => deadZone;
    public float InputSmoothing => inputSmoothing;
    public bool NormalizeDiagonalInput => normalizeDiagonalInput;
}
