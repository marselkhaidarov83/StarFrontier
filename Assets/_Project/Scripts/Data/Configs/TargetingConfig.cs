using UnityEngine;

// Конфиг TargetingConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "TargetingConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Targeting")]
public sealed class TargetingConfig : BaseConfig
{
    [Header("Selection")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Максимальное значение параметра maxTargetDistance. Используется как верхняя граница диапазона.")]
    private float maxTargetDistance = 6f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Радиус для параметра tapSelectRadiusWorld. Используется при расчёте расстояний и зон действия.")]
    private float tapSelectRadiusWorld = 0.75f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Радиус для параметра autoSelectRadiusWorld. Используется при расчёте расстояний и зон действия.")]
    private float autoSelectRadiusWorld = 3.5f;

    [SerializeField]
    [Tooltip("Переключатель preferInteractableTargets. Включает или выключает соответствующее правило или отображение.")]
    private bool preferInteractableTargets = true;

    [Header("Filtering")]
    [SerializeField]
    [Tooltip("Переключатель allowPlanets. Включает или выключает соответствующее правило или отображение.")]
    private bool allowPlanets = true;

    [SerializeField]
    [Tooltip("Переключатель allowStations. Включает или выключает соответствующее правило или отображение.")]
    private bool allowStations = true;

    [SerializeField]
    [Tooltip("Переключатель allowTravelPoints. Включает или выключает соответствующее правило или отображение.")]
    private bool allowTravelPoints = true;

    [SerializeField]
    [Tooltip("Переключатель allowEnemies. Включает или выключает соответствующее правило или отображение.")]
    private bool allowEnemies = false;

    [Header("UI")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр markerWorldScale. Используется связанными игровыми системами этого конфига.")]
    private float markerWorldScale = 1f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Скорость для параметра markerPulseSpeed. Используется при движении или анимации.")]
    private float markerPulseSpeed = 2.5f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Параметр unavailableTargetAlpha. Используется связанными игровыми системами этого конфига.")]
    private float unavailableTargetAlpha = 0.35f;

    public float MaxTargetDistance => maxTargetDistance;
    public float TapSelectRadiusWorld => tapSelectRadiusWorld;
    public float AutoSelectRadiusWorld => autoSelectRadiusWorld;
    public bool PreferInteractableTargets => preferInteractableTargets;

    public bool AllowPlanets => allowPlanets;
    public bool AllowStations => allowStations;
    public bool AllowTravelPoints => allowTravelPoints;
    public bool AllowEnemies => allowEnemies;

    public float MarkerWorldScale => markerWorldScale;
    public float MarkerPulseSpeed => markerPulseSpeed;
    public float UnavailableTargetAlpha => unavailableTargetAlpha;
}
