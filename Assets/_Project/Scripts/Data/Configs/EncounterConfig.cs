using UnityEngine;

[CreateAssetMenu(fileName = "EncounterConfig", menuName = "StarFrontier/Configs/Encounter")]
public class EncounterConfig : BaseConfig
{
    [Header("Encounter Setup")]
    [SerializeField] public EnemyConfig[] enemies;
    [SerializeField] public int difficulty;
    [SerializeField] public RewardConfig reward;

    public EnemyConfig[] Enemies => enemies;
    public int Difficulty => difficulty;
    public RewardConfig Reward => reward;
}