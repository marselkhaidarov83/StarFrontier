using System;
using System.IO;

/// <summary>
/// Изменение состояния перемещения NPC по системе или маршруту.
/// </summary>
public readonly struct SystemNpcTravelStateChangedEvent
{
    /// <summary>Идентификатор NPC в текущем игровом состоянии.</summary>
    public readonly string RuntimeNpcId;
    /// <summary>Ссылка на данные NPC, относящегося к событию.</summary>
    public readonly SystemNpcRuntimeState Npc;
    /// <summary>Состояние перемещения.</summary>
    public readonly SystemNpcTravelState TravelState;
    /// <summary>Идентификатор системы назначения.</summary>
    public readonly String DestinationSystemId;

    public SystemNpcTravelStateChangedEvent(
        string runtimeNpcId,
        SystemNpcRuntimeState npc,
        SystemNpcTravelState travelState,
        String destinationSystemId)
    {
        RuntimeNpcId = runtimeNpcId;
        Npc = npc;
        TravelState = travelState;
        DestinationSystemId = destinationSystemId;
    }
}