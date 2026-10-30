using UnityEngine;

public readonly struct SpaceLootDestroyedEvent
{
    public readonly string LootId;
    public readonly string SystemId;
    public readonly Vector3 Position;
    public readonly SystemNpcType SourceNpcType;
    public readonly string SourceNpcConfigId;

    public SpaceLootDestroyedEvent(SpaceLootRuntimeState loot)
    {
        LootId = loot.LootId;
        SystemId = loot.SystemId;
        Position = loot.Position;
        SourceNpcType = loot.SourceNpcType;
        SourceNpcConfigId = loot.SourceNpcConfigId;
    }
}
