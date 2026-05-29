using System;
using System.Collections.Generic;

[Serializable]
public class ShipRuntimeState
{
    public string ActiveShipId;
    public List<ShipRuntimeData> OwnedShips = new();

    public ShipRuntimeData GetActiveShip()
    {
        if (string.IsNullOrEmpty(ActiveShipId))
            return null;

        return OwnedShips.Find(ship => ship.ShipId == ActiveShipId);
    }

    public bool OwnsShip(string shipId)
    {
        if (string.IsNullOrEmpty(shipId))
            return false;

        return OwnedShips.Exists(ship => ship.ShipId == shipId);
    }

    public ShipRuntimeData GetOwnedShip(string shipId)
    {
        if (string.IsNullOrEmpty(shipId))
            return null;

        return OwnedShips.Find(ship => ship.ShipId == shipId);
    }

    public void AddOwnedShip(ShipRuntimeData shipState)
    {
        if (shipState == null)
            return;

        if (string.IsNullOrEmpty(shipState.ShipId))
            return;

        if (OwnsShip(shipState.ShipId))
            return;

        OwnedShips.Add(shipState);
    }
}
