using UnityEngine;

[CreateAssetMenu(
    fileName = "CurrentSystemNpcSimulationConfig",
    menuName = "StarFrontier/Configs/Npc/Current System NPC Simulation")]
public sealed class CurrentSystemNpcSimulationConfig : ScriptableObject
{
    [Header("Route Build Limits")]
    [SerializeField]
    [Range(1, 500)]
    private int maxInitialRouteBuildsPerTick = 2;

    [SerializeField]
    [Min(0f)]
    private float routeBuildTimeBudgetMsPerTick = 2f;

    [SerializeField]
    [Min(0f)]
    private float routeRefreshTimeBudgetMsPerTick = 0.75f;

    [SerializeField]
    [Min(0f)]
    private float ensureDirectionRouteBuildBudgetSafetyMs = 0.05f;

    [SerializeField]
    [Range(0, 100)]
    private int priorityFinalApproachRouteBuildsPerTick = 4;

    [SerializeField]
    [Range(1, 10)]
    private int finalPlanetApproachRefreshTicks = 3;

    [SerializeField]
    [Min(0)]
    private int maxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild = 100;

    [Header("Route Build Failure Recovery")]
    [SerializeField]
    [Range(1, 20)]
    private int routeTargetRerollAttempts = 6;

    [SerializeField]
    [Min(0f)]
    private float routeTargetRerollMinDistance = 1f;

    [Header("Completion Limits")]
    [SerializeField]
    [Range(1, 100)]
    private int maxSystemExitCompletionsPerTick = 2;

    public int MaxInitialRouteBuildsPerTick =>
        Mathf.Clamp(maxInitialRouteBuildsPerTick, 1, 500);

    public float RouteBuildTimeBudgetMsPerTick =>
        Mathf.Max(0f, routeBuildTimeBudgetMsPerTick);

    public float RouteRefreshTimeBudgetMsPerTick =>
        Mathf.Max(0f, routeRefreshTimeBudgetMsPerTick);

    public float EnsureDirectionRouteBuildBudgetSafetyMs =>
        Mathf.Max(0f, ensureDirectionRouteBuildBudgetSafetyMs);

    public int PriorityFinalApproachRouteBuildsPerTick =>
        Mathf.Clamp(priorityFinalApproachRouteBuildsPerTick, 0, 100);

    public int FinalPlanetApproachRefreshTicks =>
        Mathf.Clamp(finalPlanetApproachRefreshTicks, 1, 10);

    public int MaxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild =>
        Mathf.Max(0, maxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild);

    public int RouteTargetRerollAttempts =>
        Mathf.Clamp(routeTargetRerollAttempts, 1, 20);

    public float RouteTargetRerollMinDistance =>
        Mathf.Max(0f, routeTargetRerollMinDistance);

    public int MaxSystemExitCompletionsPerTick =>
        Mathf.Clamp(maxSystemExitCompletionsPerTick, 1, 100);

    private void OnValidate()
    {
        maxInitialRouteBuildsPerTick =
            Mathf.Clamp(maxInitialRouteBuildsPerTick, 1, 500);

        routeBuildTimeBudgetMsPerTick =
            Mathf.Max(0f, routeBuildTimeBudgetMsPerTick);

        routeRefreshTimeBudgetMsPerTick =
            Mathf.Max(0f, routeRefreshTimeBudgetMsPerTick);

        ensureDirectionRouteBuildBudgetSafetyMs =
            Mathf.Max(0f, ensureDirectionRouteBuildBudgetSafetyMs);

        priorityFinalApproachRouteBuildsPerTick =
            Mathf.Clamp(priorityFinalApproachRouteBuildsPerTick, 0, 100);

        finalPlanetApproachRefreshTicks =
            Mathf.Clamp(finalPlanetApproachRefreshTicks, 1, 10);

        maxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild =
            Mathf.Max(0, maxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild);

        routeTargetRerollAttempts =
            Mathf.Clamp(routeTargetRerollAttempts, 1, 20);

        routeTargetRerollMinDistance =
            Mathf.Max(0f, routeTargetRerollMinDistance);

        maxSystemExitCompletionsPerTick =
            Mathf.Clamp(maxSystemExitCompletionsPerTick, 1, 100);
    }
}