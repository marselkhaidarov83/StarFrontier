using UnityEngine;

public readonly struct SystemNpcDestroyedEvent
{
    public readonly string RuntimeNpcGroupId;
    public readonly string RuntimeNpcId;
    public readonly SystemNpcType NpcType;
    public readonly string SystemId;
    public readonly bool WasKilledByPlayer;
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