using UnityEngine;

[CreateAssetMenu(fileName = "spaceLootCombatConfig",
    menuName = "StarFrontier/Configs/Combat/Space Loot Combat")]
public sealed class SpaceLootCombatConfig : ScriptableObject
{
    [SerializeField, Min(1)] private int maxHealth = 1;

    public int MaxHealth => Mathf.Max(1, maxHealth);
}
