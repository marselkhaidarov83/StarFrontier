using UnityEngine;

public readonly struct SystemTravelCompletedEvent
{
    public readonly TravelDestinationType DestinationType;
    public readonly Vector3 FinalPosition;
    public readonly string PlanetId;
    public readonly string TargetSystemId;
    public readonly StarSystemConfig TargetSystemConfig;
    public readonly StarSystemLink SystemLink;

    public SystemTravelCompletedEvent(
        TravelDestinationType destinationType,
        Vector3 finalPosition,
        string planetId,
        string targetSystemId,
        StarSystemConfig targetSystemConfig,
        StarSystemLink systemLink)
    {
        DestinationType = destinationType;
        FinalPosition = finalPosition;
        PlanetId = planetId;
        TargetSystemId = targetSystemId;
        TargetSystemConfig = targetSystemConfig;
        SystemLink = systemLink;
    }
}