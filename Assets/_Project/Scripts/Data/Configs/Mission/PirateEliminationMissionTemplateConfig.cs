using UnityEngine;

[CreateAssetMenu(fileName = "mission_elimination_pirates", menuName = "StarFrontier/Configs/Mission/Pirate Elimination Template")]
public sealed class PirateEliminationMissionTemplateConfig : MissionTemplateConfig
{
    public override MissionType MissionType => global::MissionType.Elimination;
}
