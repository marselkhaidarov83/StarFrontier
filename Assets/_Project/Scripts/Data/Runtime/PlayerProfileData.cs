using System;
using UnityEngine;

[Serializable]
public class PlayerProfileData
{
    public string PlayerId = "local_player";
    public string PlayerName = "Pilot";
    public int Level = 1;
    public int Experience = 0;    
    public int Credits = 1000;
    public string CurrentSystemId;
    public string CurrentPlanetId;
    public bool IsOnPlanet() { return !string.IsNullOrEmpty(CurrentPlanetId); }
    public Vector3 SystemMapShipPosition = new Vector3(0, 0, -2);
    public ShipRuntimeState PlayerShipState = new();
    public ShipRuntimeData GetActiveShip() { return PlayerShipState.GetActiveShip(); }
}