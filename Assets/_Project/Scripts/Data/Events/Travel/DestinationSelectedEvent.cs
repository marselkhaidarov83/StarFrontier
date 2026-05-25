using UnityEngine;

public readonly struct DestinationSelectedEvent
{
    public readonly TravelDestinationType DestinationType;
    public readonly Vector2 DestinationPosition;
    public readonly string PlanetId;
    public readonly string TargetSystemId;

    public DestinationSelectedEvent(
        TravelDestinationType destinationType,
        Vector2 destinationPosition,
        string planetId,
        string targetSystemId)
    {
        DestinationType = destinationType;
        DestinationPosition = destinationPosition;
        PlanetId = planetId;
        TargetSystemId = targetSystemId;
    }
}