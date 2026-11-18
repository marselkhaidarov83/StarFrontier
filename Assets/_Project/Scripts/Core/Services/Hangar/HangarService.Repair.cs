using System;
using System.Collections.Generic;

public partial class HangarService
{
    private sealed class RepairPlan
    {
        public bool RepairActiveHull;
        public HullEquipmentRuntimeState StoredHull;
        public readonly List<SystemNpcWeaponRuntimeState> Equipment = new();
        public long Price;
    }

    public long GetRepairPrice(HangarRepairScope scope, int slot, string itemId = null, bool warehouse = false) =>
        BuildRepairPlan(GetActiveShipState(), _market.CurrentMarket, scope, slot, itemId, warehouse)?.Price ?? 0;

    public bool TryRepair(HangarRepairScope scope, int slot, string itemId, bool warehouse, long quotedPrice, out string reason)
    {
        reason = "Ремонт недоступен.";
        if (_changingLoadout) return false;
        _changingLoadout = true;
        try
        {
            var player = _gameSessionService.State?.Player;
            var ship = GetActiveShipState();
            var market = _market.CurrentMarket;
            var plan = BuildRepairPlan(ship, market, scope, slot, itemId, warehouse);
            if (player == null || plan == null || plan.Price <= 0 || plan.Price > int.MaxValue) return false;
            if (plan.Price != quotedPrice)
            {
                reason = "Стоимость изменилась. Проверь новую цену.";
                return false;
            }
            if (player.Credits < plan.Price)
            {
                reason = "Недостаточно кредитов.";
                return false;
            }

            player.Credits -= (int)plan.Price;
            if (plan.RepairActiveHull)
            {
                ship.CurrentHull = ship.HullCapacity;
                RestoreHull(ship.InstalledHull);
            }
            RestoreHull(plan.StoredHull);
            foreach (var item in plan.Equipment) item.CurrentDurability = item.MaximumDurability;
            market.Revision++;

            if (plan.RepairActiveHull)
            {
                PublishLoadoutEvent(new ShipRepairedEvent(ship.ShipId));
                PublishLoadoutEvent(new ShipStatsChangedEvent(ship.ShipId));
            }
            PublishLoadoutEvent(new CreditsChangedEvent(player.Credits));
            PublishLoadoutEvent(new MarketStateChangedEvent(market.SystemId, market.PlanetId));
            PublishLoadoutEvent(new SaveNeedEvent("hangar_repair"));
            AppLog.Info(DebugLogChannel.Hangar, typeof(HangarService), "Repair completed | Scope=" + scope + " | Price=" + plan.Price);
            reason = string.Empty;
            return true;
        }
        finally { _changingLoadout = false; }
    }

    private RepairPlan BuildRepairPlan(ShipRuntimeData ship, MarketRuntimeData market, HangarRepairScope scope,
        int slot, string itemId, bool warehouse)
    {
        var prices = _configService.MarketEconomy;
        if (ship?.HasModularLoadout != true || market == null || prices == null) return null;

        var plan = new RepairPlan();
        bool hullSelected = slot == ShipLoadoutRules.HullSlot;
        if (scope == HangarRepairScope.FullShip || (scope == HangarRepairScope.Installed && hullSelected))
        {
            long missing = (long)ship.HullCapacity - Math.Max(0, ship.CurrentHull);
            if (missing > 0)
            {
                if (prices.HullRepairUnitPrice <= 0) return null;
                plan.RepairActiveHull = true;
                plan.Price = missing * prices.HullRepairUnitPrice;
            }
        }

        int equipmentPrice = prices.EquipmentRepairUnitPrice;
        if (scope == HangarRepairScope.FullShip)
        {
            foreach (var item in ship.InstalledWeapons)
                if (!AddEquipmentRepair(plan, item, equipmentPrice)) return null;
            foreach (var item in ship.InstalledComponents)
                if (!AddEquipmentRepair(plan, item, equipmentPrice)) return null;
        }
        else if (scope == HangarRepairScope.Installed && !hullSelected)
        {
            if (!AddEquipmentRepair(plan, SelectedEquipment(ship, market, slot, null, false), equipmentPrice)) return null;
        }
        else if (scope == HangarRepairScope.Inventory && !string.IsNullOrEmpty(itemId))
        {
            if (hullSelected)
            {
                var hull = SelectedStoredHull(market, itemId, warehouse);
                if (hull == null || hull.MaximumDurability <= 0) return null;
                int missing = hull.MaximumDurability - hull.CurrentDurability;
                if (missing > 0)
                {
                    if (prices.HullRepairUnitPrice <= 0) return null;
                    plan.StoredHull = hull;
                    plan.Price = (long)missing * prices.HullRepairUnitPrice;
                }
            }
            else if (!AddEquipmentRepair(plan, SelectedEquipment(ship, market, slot, itemId, warehouse), equipmentPrice))
                return null;
        }
        return plan;
    }

    private static bool AddEquipmentRepair(RepairPlan plan, SystemNpcWeaponRuntimeState item, int unitPrice)
    {
        if (item == null || item.CurrentDurability >= item.MaximumDurability) return true;
        if (unitPrice <= 0) return false;
        long missing = (long)item.MaximumDurability - Math.Max(0, item.CurrentDurability);
        long cost = missing * unitPrice;
        if (cost > int.MaxValue - plan.Price) return false;
        plan.Equipment.Add(item);
        plan.Price += cost;
        return true;
    }

    private static void RestoreHull(HullEquipmentRuntimeState hull)
    {
        if (hull != null) hull.CurrentDurability = hull.MaximumDurability;
    }

    private static HullEquipmentRuntimeState SelectedStoredHull(MarketRuntimeData market, string itemId, bool warehouse) =>
        warehouse ? market.PersonalHulls?.Find(x => x?.InstanceId == itemId) : null;

    private SystemNpcWeaponRuntimeState SelectedEquipment(ShipRuntimeData ship, MarketRuntimeData market,
        int slot, string itemId, bool warehouse)
    {
        if (slot < ShipLoadoutRules.FirstComponentSlot || slot >= ShipLoadoutRules.TotalSlots) return null;
        bool component = slot < ShipLoadoutRules.FirstWeaponSlot;
        if (string.IsNullOrEmpty(itemId))
            return component
                ? ShipLoadoutRules.Component(ship, (ShipComponentSlot)(slot - ShipLoadoutRules.FirstComponentSlot))
                : ShipLoadoutRules.Weapon(ship, slot - ShipLoadoutRules.FirstWeaponSlot);

        var inventory = warehouse ? market.PersonalEquipment : ship.CargoEquipment;
        var item = inventory?.Find(x => x?.InstanceId == itemId && x.IsComponent == component);
        if (component && item != null &&
            GetComponent(item.ComponentConfigId)?.Slot != (ShipComponentSlot)(slot - ShipLoadoutRules.FirstComponentSlot)) return null;
        return item;
    }

    public bool TryDebugDamageSelection(int slot, string itemId, bool warehouse, out string reason)
    {
        reason = "Выбери оборудование с ненулевой прочностью.";
        if (_changingLoadout) return false;
        _changingLoadout = true;
        try
        {
            const int damage = 10;
            var ship = GetActiveShipState();
            var market = _market.CurrentMarket;
            if (ship?.HasModularLoadout != true || market == null) return false;
            bool activeHull = slot == ShipLoadoutRules.HullSlot && string.IsNullOrEmpty(itemId);
            int before;
            int after;
            if (slot == ShipLoadoutRules.HullSlot)
            {
                var hull = activeHull ? ship.InstalledHull : SelectedStoredHull(market, itemId, warehouse);
                if (hull == null) return false;
                before = activeHull ? ship.CurrentHull : hull.CurrentDurability;
                if (before <= 0) return false;
                after = Math.Max(0, before - damage);
                hull.CurrentDurability = after;
                if (activeHull) ship.CurrentHull = after;
            }
            else
            {
                var item = SelectedEquipment(ship, market, slot, itemId, warehouse);
                if (item == null || item.CurrentDurability <= 0) return false;
                before = item.CurrentDurability;
                after = Math.Max(0, before - damage);
                item.CurrentDurability = after;
            }

            market.Revision++;
            if (activeHull) PublishLoadoutEvent(new ShipStatsChangedEvent(ship.ShipId));
            PublishLoadoutEvent(new MarketStateChangedEvent(market.SystemId, market.PlanetId));
            PublishLoadoutEvent(new SaveNeedEvent("hangar_debug_damage"));
            reason = $"Урон {before - after} ед. Прочность {before} → {after}.";
            AppLog.Info(DebugLogChannel.Hangar, typeof(HangarService), reason);
            return true;
        }
        finally { _changingLoadout = false; }
    }
}
