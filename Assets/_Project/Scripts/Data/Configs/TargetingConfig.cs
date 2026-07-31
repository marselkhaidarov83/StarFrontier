using UnityEngine;

[CreateAssetMenu(
    fileName = "TargetingConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Targeting")]
public sealed class TargetingConfig : ScriptableObject
{
    [Header("Selection")]
    [SerializeField]
    [Min(0f)]
    private float maxTargetDistance = 6f;

    [SerializeField]
    [Min(0f)]
    private float tapSelectRadiusWorld = 0.75f;

    [SerializeField]
    [Min(0f)]
    private float autoSelectRadiusWorld = 3.5f;

    [SerializeField]
    private bool preferInteractableTargets = true;

    [Header("Filtering")]
    [SerializeField]
    private bool allowPlanets = true;

    [SerializeField]
    private bool allowStations = true;

    [SerializeField]
    private bool allowTravelPoints = true;

    [SerializeField]
    private bool allowEnemies = false;

    [Header("UI")]
    [SerializeField]
    [Min(0f)]
    private float markerWorldScale = 1f;

    [SerializeField]
    [Min(0f)]
    private float markerPulseSpeed = 2.5f;

    [SerializeField]
    [Range(0f, 1f)]
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