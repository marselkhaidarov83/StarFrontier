using UnityEngine;

public readonly struct SystemTravelProgressChangedEvent
{
    public readonly Vector2 CurrentPosition;
    public readonly Vector2 DestinationPosition;
    public readonly float Progress01;

    public SystemTravelProgressChangedEvent(
        Vector2 currentPosition,
        Vector2 destinationPosition,
        float progress01)
    {
        CurrentPosition = currentPosition;
        DestinationPosition = destinationPosition;
        Progress01 = progress01;
    }
}