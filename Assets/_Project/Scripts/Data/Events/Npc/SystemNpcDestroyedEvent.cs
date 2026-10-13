using UnityEngine;

/// <summary>
/// Уничтожение NPC; используется для удаления объекта и связанных отображений.
/// </summary>
public readonly struct SystemNpcDestroyedEvent
{
    /// <summary>Идентификатор группы NPC в runtime-состоянии.</summary>
    public readonly string RuntimeNpcGroupId;
    /// <summary>Идентификатор NPC в текущем игровом состоянии.</summary>
    public readonly string RuntimeNpcId;
    /// <summary>Тип NPC.</summary>
    public readonly SystemNpcType NpcType;
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;
    /// <summary>Признак уничтожения цели игроком.</summary>
    public readonly bool WasKilledByPlayer;
    /// <summary>Позиция объекта в игровом пространстве.</summary>
    public readonly Vector3 Position;

    public SystemNpcDestroyedEvent(
        string runtimeNpcGroupId,
        string runtimeNpcId,
        SystemNpcType npcType,
        string systemId,
        bool wasKilledByPlayer,
        Vector3 position)
    {
        RuntimeNpcGroupId = runtimeNpcGroupId;
        RuntimeNpcId = runtimeNpcId;
        NpcType = npcType;
        SystemId = systemId;
        WasKilledByPlayer = wasKilledByPlayer;
        Position = position;
    }
}