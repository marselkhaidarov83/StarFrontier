using System;
using UnityEngine;

[Serializable]
public sealed class EnemyAstraliteEntry
{
    public EnemyFaction2A Species;
    public ItemConfig Item;
}

[CreateAssetMenu(fileName = "enemy_astralite_catalog_", menuName = "StarFrontier/Configs/Npc/Enemy Astralite Catalog")]
public sealed class EnemyAstraliteCatalogConfig : ScriptableObject
{
    [SerializeField] private EnemyAstraliteEntry[] entries = Array.Empty<EnemyAstraliteEntry>();

    public EnemyAstraliteEntry[] Entries => entries;

    public ItemConfig GetItem(EnemyFaction2A species)
    {
        if (entries == null) return null;
        foreach (EnemyAstraliteEntry entry in entries)
            if (entry != null && entry.Species == species)
                return entry.Item;
        return null;
    }
}
