using UnityEngine;

[CreateAssetMenu(
    fileName = "InteractionConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Interaction")]
public sealed class InteractionConfig : ScriptableObject
{
    [Header("Distances")]
    [SerializeField]
    [Min(0f)]
    private float planetInteractionDistance = 2.2f;

    [SerializeField]
    [Min(0f)]
    private float stationInteractionDistance = 2.4f;

    [SerializeField]
    [Min(0f)]
    private float travelPointInteractionDistance = 1.8f;

    [Header("Timing")]
    [SerializeField]
    [Min(0f)]
    private float holdToInteractSeconds = 0.25f;

    [SerializeField]
    [Min(0f)]
    private float interactionCooldownSeconds = 0.5f;

    [Header("Rules")]
    [SerializeField]
    private bool requireSelectedTarget = true;

    [SerializeField]
    private bool autoSelectNearestIfNoneSelected = true;

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