using UnityEngine;

[CreateAssetMenu(
    fileName = "InteractionConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Interaction")]
public sealed class InteractionConfig : BaseConfig
{
    [Header("Дистанции")]
    [Tooltip(
        "Дистанция, с которой игрок может взаимодействовать с планетой. " +
        "Используется системой взаимодействия при проверке доступной цели.")]
    [SerializeField]
    [Min(0f)]
    private float planetInteractionDistance = 2.2f;

    [Tooltip(
        "Дистанция, с которой игрок может взаимодействовать со станцией. " +
        "Используется системой взаимодействия при проверке доступной цели.")]
    [SerializeField]
    [Min(0f)]
    private float stationInteractionDistance = 2.4f;

    [Tooltip(
        "Дистанция, с которой игрок может взаимодействовать с точкой перехода. " +
        "Используется системой взаимодействия при проверке доступной цели.")]
    [SerializeField]
    [Min(0f)]
    private float travelPointInteractionDistance = 1.8f;

    [Header("Время")]
    [Tooltip(
        "Сколько секунд нужно удерживать действие, чтобы взаимодействие сработало. " +
        "Используется при обработке нажатия игрока.")]
    [SerializeField]
    [Min(0f)]
    private float holdToInteractSeconds = 0.25f;

    [Tooltip(
        "Задержка между повторными взаимодействиями. " +
        "Используется, чтобы действие не срабатывало несколько раз подряд случайно.")]
    [SerializeField]
    [Min(0f)]
    private float interactionCooldownSeconds = 0.5f;

    [Header("Правила")]
    [Tooltip(
        "Требует, чтобы цель была выбрана перед взаимодействием. " +
        "Используется системой взаимодействия перед выполнением действия.")]
    [SerializeField]
    private bool requireSelectedTarget = true;

    [Tooltip(
        "Если цель не выбрана, разрешает автоматически выбрать ближайшую подходящую цель. " +
        "Используется системой взаимодействия при попытке действия без выбранного объекта.")]
    [SerializeField]
    private bool autoSelectNearestIfNoneSelected = true;

    [Tooltip(
        "Останавливает корабль игрока при успешном взаимодействии. " +
        "Используется после выполнения действия с планетой, станцией или точкой перехода.")]
    [SerializeField]
    private bool pauseShipOnInteraction = true;

    public float PlanetInteractionDistance => planetInteractionDistance;
    public float StationInteractionDistance => stationInteractionDistance;
    public float TravelPointInteractionDistance => travelPointInteractionDistance;

    public float HoldToInteractSeconds => holdToInteractSeconds;
    public float InteractionCooldownSeconds => interactionCooldownSeconds;

    public bool RequireSelectedTarget => requireSelectedTarget;
    public bool AutoSelectNearestIfNoneSelected => autoSelectNearestIfNoneSelected;
    public bool PauseShipOnInteraction => pauseShipOnInteraction;
}