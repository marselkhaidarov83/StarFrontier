using UnityEngine;

public readonly struct SystemTravelStartedEvent
{
    public readonly TravelDestinationType DestinationType;
    public readonly Vector2 StartPosition;
    public readonly Vector2 DestinationPosition;

    public SystemTravelStartedEvent(
        TravelDestinationType destinationType,
        Vector2 startPosition,
        Vector2 destinationPosition)
    {
        DestinationType = destinationType;
        StartPosition = startPosition;
        DestinationPosition = destinationPosition;
    }
}
