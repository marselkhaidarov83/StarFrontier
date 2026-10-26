using UnityEngine;

[CreateAssetMenu(fileName = "mission_route", menuName = "StarFrontier/Configs/Mission/Route Template")]
public sealed class RouteMissionTemplateConfig : MissionTemplateConfig
{
    public override MissionType MissionType => global::MissionType.RouteSearch;
    [Tooltip("Выключено — открыть скрытый прямой маршрут; включено — повысить уровень открытого.")]
    [SerializeField] private bool improveExistingRoute;
    public bool ImproveExistingRoute => improveExistingRoute;
}
