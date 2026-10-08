using UnityEngine;

// Конфиг SystemCameraConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "SystemCameraConfig",
    menuName = "StarFrontier/Configs/Game/System Camera"
)]
public sealed class SystemCameraConfig : BaseConfig
{
    [Header("Follow")]
    [Tooltip("Параметр followSmoothTime. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float followSmoothTime = 0.18f;
    [Tooltip("Параметр returnSmoothTime. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float returnSmoothTime = 0.12f;
    [Tooltip("Параметр followDeadZone. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float followDeadZone = 8f;

    [Header("View Size")]
    [Tooltip("Параметр defaultOrthographicSize. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float defaultOrthographicSize = 1200f;
    [Tooltip("Минимальное значение параметра minOrthographicSize. Используется как нижняя граница диапазона.")]
    [SerializeField] private float minOrthographicSize = 650f;
    [Tooltip("Максимальное значение параметра maxOrthographicSize. Используется как верхняя граница диапазона.")]
    [SerializeField] private float maxOrthographicSize = 1900f;

    [Header("New Game Start Frame")]
    [Tooltip("Переключатель useNewGameStartFrame. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool useNewGameStartFrame = true;
    [Tooltip("Параметр startFrameShipBottomViewportPercent. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] [Range(0.01f, 0.49f)] private float startFrameShipBottomViewportPercent = 0.2f;
    [Tooltip("Параметр startFrameSunBottomViewportPercent. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] [Range(0.51f, 0.99f)] private float startFrameSunBottomViewportPercent = 0.8f;
    [Tooltip("Минимальное значение параметра startFrameMinOrthographicSize. Используется как нижняя граница диапазона.")]
    [SerializeField] [Min(1f)] private float startFrameMinOrthographicSize = 1f;

    [Header("System Bounds")]
    [Tooltip("Параметр boundsPadding. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float boundsPadding = 320f;
    [Tooltip("Параметр sunExtraPadding. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float sunExtraPadding = 220f;
    [Tooltip("Параметр planetExtraPadding. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float planetExtraPadding = 180f;
    [Tooltip("Параметр stationExtraPadding. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float stationExtraPadding = 220f;
    [Tooltip("Параметр exitExtraPadding. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float exitExtraPadding = 220f;

    [Header("Free Look")]
    [Tooltip("Параметр dragSensitivity. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float dragSensitivity = 1f;
    [Tooltip("Параметр freeLookInertia. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float freeLookInertia = 0f;

    public float FollowSmoothTime => Mathf.Max(0.01f, followSmoothTime);
    public float ReturnSmoothTime => Mathf.Max(0.01f, returnSmoothTime);
    public float FollowDeadZone => Mathf.Max(0f, followDeadZone);

    public float DefaultOrthographicSize => Mathf.Max(1f, defaultOrthographicSize);
    public float MinOrthographicSize => Mathf.Max(1f, minOrthographicSize);
    public float MaxOrthographicSize => Mathf.Max(MinOrthographicSize, maxOrthographicSize);
    public bool UseNewGameStartFrame => useNewGameStartFrame;
    public float StartFrameShipBottomViewportPercent =>
        Mathf.Clamp(startFrameShipBottomViewportPercent, 0.01f, 0.49f);
    public float StartFrameSunBottomViewportPercent =>
        Mathf.Clamp(startFrameSunBottomViewportPercent, 0.51f, 0.99f);
    public float StartFrameMinOrthographicSize =>
        Mathf.Max(1f, startFrameMinOrthographicSize);

    public float BoundsPadding => Mathf.Max(0f, boundsPadding);
    public float SunExtraPadding => Mathf.Max(0f, sunExtraPadding);
    public float PlanetExtraPadding => Mathf.Max(0f, planetExtraPadding);
    public float StationExtraPadding => Mathf.Max(0f, stationExtraPadding);
    public float ExitExtraPadding => Mathf.Max(0f, exitExtraPadding);

    public float DragSensitivity => Mathf.Max(0.01f, dragSensitivity);
    public float FreeLookInertia => Mathf.Max(0f, freeLookInertia);
}
