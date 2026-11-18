using UnityEngine;

public abstract class SpaceLootCombatConfig : BaseConfig
{
    [Header("Космический лут")]
    [SerializeField, Min(1), Tooltip("Здоровье объекта после выброса в космос.")]
    private int lootHealth = 1;

    public int LootHealth => Mathf.Max(1, lootHealth);
}
