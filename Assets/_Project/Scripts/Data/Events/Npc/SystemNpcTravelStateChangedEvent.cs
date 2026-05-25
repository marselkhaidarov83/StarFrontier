using System;
using System.IO;

public readonly struct SystemNpcTravelStateChangedEvent
{
    public readonly string RuntimeNpcId;
    public readonly SystemNpcRuntimeState Npc;
    public readonly SystemNpcTravelState TravelState;
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