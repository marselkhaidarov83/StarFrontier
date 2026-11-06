public interface IStationFacilityService
{
    bool TryRefuel(out string reason);
    bool TryRepairHull(out string reason);
    bool TryBuy(string itemId, int quantity, out string reason);
    bool TrySell(string itemId, int quantity, out string reason);
}
