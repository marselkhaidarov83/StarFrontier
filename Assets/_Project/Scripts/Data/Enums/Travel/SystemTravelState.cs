using System;
using UnityEngine;

[Serializable]
public class SystemTravelState
{
    public string CurrentSystemId;
    public string CurrentPlanetId;

    private Vector3 _currentPosition;
    public Vector3 GetCurrentPosition() { return _currentPosition; }
    public void SetCurrentPosition(Vector3 currentPosition) 
    {
         _currentPosition = currentPosition;
         _currentPosition.z = -2;
    }

    public Vector3 StartPosition;
    public Vector3 DestinationPosition;

    // public float TravelSpeedUnitsPerSecond = 120f;
    public float TravelDistance;
    public float TravelProgress01;

    public SystemTravelStatus Status = SystemTravelStatus.Idle;
    public SystemTravelDestination Destination = SystemTravelDestination.None();

    public bool IsFlying => Status == SystemTravelStatus.Flying;
    public bool HasDestination => Destination != null && Destination.HasDestination;
}