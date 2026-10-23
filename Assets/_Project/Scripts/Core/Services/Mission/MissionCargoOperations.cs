using System;
using System.Linq;

public static class MissionCargoOperations
{
    public static bool HasCargo(GameRuntimeState state, MissionInstanceData mission, bool activeShipOnly)
    {
        var shipState = state?.Player?.PlayerShipState;
        if (shipState?.OwnedShips == null || mission?.Objective == null) return false;
        return shipState.OwnedShips.Any(ship => ship != null && (!activeShipOnly || ship.ShipId == shipState.ActiveShipId) &&
            ship.Cargo?.entries?.Where(e => e != null && e.MissionRuntimeId == mission.MissionRuntimeId &&
                e.ItemId == mission.Objective.MissionCargoItemId).Sum(e => (long)Math.Max(0, e.Quantity)) == mission.Objective.MissionCargoAmount);
    }

    public static bool Grant(GameRuntimeState state, MissionInstanceData mission, IInventoryService inventory)
    {
        var objective = mission?.Objective;
        var cargo = state?.Player?.PlayerShipState?.GetActiveShip()?.Cargo;
        if (objective == null || cargo?.entries == null || objective.MissionCargoAmount <= 0 ||
            !inventory.CanAddItem(objective.MissionCargoItemId, objective.MissionCargoAmount) ||
            cargo.entries.Any(e => e != null && e.MissionRuntimeId == mission.MissionRuntimeId)) return false;
        cargo.entries.Add(new RuntimeCargoEntryData(objective.MissionCargoItemId, objective.MissionCargoAmount)
            { MissionRuntimeId = mission.MissionRuntimeId });
        return true;
    }

    public static void Remove(GameRuntimeState state, MissionInstanceData mission)
    {
        if (state?.Player?.PlayerShipState?.OwnedShips == null || mission == null) return;
        foreach (var ship in state.Player.PlayerShipState.OwnedShips)
            ship?.Cargo?.entries?.RemoveAll(e => e != null && e.MissionRuntimeId == mission.MissionRuntimeId);
    }
}
