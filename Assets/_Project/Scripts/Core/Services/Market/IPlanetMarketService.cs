using System.Collections.Generic;

public interface IPlanetMarketService
{
    void InitializeSession();
    void AdvanceTime(double seconds);
    void AdvanceScheduledTime(double seconds, int quantTick, IGalaxyNpcSimulationScheduleService schedule);
    double ClaimOffline(long nowUtcTicks);
    void ProcessOffline(long nowUtcTicks);
    IReadOnlyList<MarketItemEntry> GetItems();
    MarketRuntimeData CurrentMarket { get; }
    int GetPrice(string itemId, bool buy);
    long GetTotalPrice(string itemId, int quantity, bool buy);
    int GetMaximumTradeQuantity(string itemId, bool buy, bool warehouse = false);
    bool CanTrade(string itemId, int quantity, bool buy, bool warehouse = false);
    EconomyOperationResult Trade(string itemId, int quantity, bool buy, string operationId = null, long expectedRevision = -1, bool warehouse = false);
    bool TradeEquipment(string instanceId, bool buy, string operationId, long expectedRevision, out string reason, bool warehouse = false);
    bool CanTradeEquipment(string instanceId, bool buy, bool warehouse = false);
    bool CanTransfer(string id, bool equipment, bool toWarehouse, int quantity);
    bool Transfer(string id, bool equipment, bool toWarehouse, int quantity, string operationId, long expectedRevision, out string reason);
    HullConfig GetHullConfig(string configId);
    int HullPrice(HullEquipmentRuntimeState hull, bool buy);
    bool CanTradeHull(string instanceId, bool buy, bool warehouse = false);
    bool TradeHull(string instanceId, bool buy, string operationId, long expectedRevision, out string reason, bool warehouse = false);
    int EquipmentPrice(SystemNpcWeaponRuntimeState equipment, bool buy);
}
