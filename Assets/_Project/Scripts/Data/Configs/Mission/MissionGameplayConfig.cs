using UnityEngine;

[CreateAssetMenu(fileName = "mission_gameplay", menuName = "StarFrontier/Configs/Mission/Gameplay")]
public sealed class MissionGameplayConfig : BaseConfig
{
    [Header("Кредиты за личное убийство при победе")]
    [Min(0)] public int DefenseKillRewardDeflator = 100;
    [Min(0)] public int LiberationKillRewardDeflator = 100;
}
