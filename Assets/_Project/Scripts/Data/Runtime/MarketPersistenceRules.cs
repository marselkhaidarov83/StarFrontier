using System;
using System.Collections.Generic;

public static class MarketPersistenceRules
{
    public static void Normalize(GameRuntimeState state)
    {
        state.Markets ??= new List<MarketRuntimeData>();
        bool legacy = (state.Meta?.SaveDataVersion ?? 0) < SaveDataVersions.EquipmentDurability;
        foreach (var ship in state.Player?.PlayerShipState?.OwnedShips ?? new List<ShipRuntimeData>())
        {
            if (ship == null) continue;
            if (ship.InstalledHull != null && HullEquipmentRuntimeState.IsEmptyPlaceholder(ship.InstalledHull))
            {
                ship.InstalledHull = null;
                AppLog.Info(DebugLogChannel.Market, "[MARKET_SAVE] Cleared empty optional InstalledHull | Ship=" + ship.ShipId);
            }
            ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
            foreach (var equipment in ship.CargoEquipment)
                if (equipment != null && string.IsNullOrEmpty(equipment.InstanceId)) equipment.InstanceId = Guid.NewGuid().ToString("N");
            ship.CargoHulls ??= new List<HullEquipmentRuntimeState>();
            ship.InstalledComponents ??= new List<SystemNpcWeaponRuntimeState>();
            ship.InstalledComponentIds ??= new List<string>();
            ship.InstalledWeapons ??= new List<SystemNpcWeaponRuntimeState>();
            if (legacy)
            {
                ship.InstalledHull?.MigrateDurability();
                foreach (var hull in ship.CargoHulls) hull?.MigrateDurability();
                foreach (var equipment in ship.CargoEquipment) equipment?.MigrateDurability();
                foreach (var weapon in ship.InstalledWeapons) weapon?.MigrateDurability();
            }
        }
        foreach (var market in state.Markets)
        {
            if (market == null) continue;
            market.Items ??= new List<MarketRuntimeItemState>();
            market.Equipment ??= new List<MarketEquipmentListing>();
            market.EquipmentSpawns ??= new List<MarketEquipmentSpawnState>();
            market.Receipts ??= new List<MarketTransactionReceipt>();
            market.PersonalStorage ??= new RuntimeCargoInventory();
            market.PersonalStorage.entries ??= new List<RuntimeCargoEntryData>();
            market.PersonalEquipment ??= new List<SystemNpcWeaponRuntimeState>();
            market.PersonalHulls ??= new List<HullEquipmentRuntimeState>();
            market.Hulls ??= new List<MarketHullListing>();
            if (legacy)
            {
                foreach (var equipment in market.PersonalEquipment) equipment?.MigrateDurability();
                foreach (var listing in market.Equipment) listing?.Equipment?.MigrateDurability();
                foreach (var hull in market.PersonalHulls) hull?.MigrateDurability();
                foreach (var listing in market.Hulls) listing?.Hull?.MigrateDurability();
            }
        }
        if (legacy && state.SystemNpcSimulation?.SpaceLoot != null)
            foreach (var loot in state.SystemNpcSimulation.SpaceLoot) loot?.Equipment?.MigrateDurability();
    }
    public static IEnumerable<string> Errors(GameRuntimeState state)
    {
        if (state.Meta == null || !Finite(state.Meta.MarketSimulationSeconds) || state.Meta.MarketSimulationSeconds < 0)
            yield return "InvalidMarketClock";
        var markets = new HashSet<string>(StringComparer.Ordinal);
        var instances = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ship in state.Player?.PlayerShipState?.OwnedShips ?? new List<ShipRuntimeData>())
            foreach (var equipment in ship?.CargoEquipment ?? new List<SystemNpcWeaponRuntimeState>())
                if (!ValidEquipment(equipment))
                    yield return "InvalidCargoEquipment";
                else if (!instances.Add(equipment.InstanceId))
                    yield return "DuplicateEquipmentInstance";
        foreach (var ship in state.Player?.PlayerShipState?.OwnedShips ?? new List<ShipRuntimeData>())
            if (ship != null && !HullEquipmentRuntimeState.IsEmptyPlaceholder(ship.InstalledHull) && (!ValidHull(ship.InstalledHull) || !instances.Add(ship.InstalledHull.InstanceId)))
                yield return "InvalidInstalledHull";
        foreach (var ship in state.Player?.PlayerShipState?.OwnedShips ?? new List<ShipRuntimeData>())
        {
            if (ship == null) continue;
            foreach (var hull in ship.CargoHulls ?? new List<HullEquipmentRuntimeState>())
                if (!ValidHull(hull) || !instances.Add(hull.InstanceId)) yield return "InvalidCargoHull";
            if (!ship.HasModularLoadout) continue;
            if (HullEquipmentRuntimeState.IsEmptyPlaceholder(ship.InstalledHull) ||
                ship.InstalledComponents == null ||
                ship.EquippedWeaponIds?.Count != ShipLoadoutRules.WeaponSlots || ship.InstalledWeapons == null)
            {
                yield return "InvalidModularLoadout";
                continue;
            }
            var slots = new HashSet<int>();
            foreach (var component in ship.InstalledComponents)
                if (!ValidEquipment(component) || !component.IsComponent || !instances.Add(component.InstanceId) ||
                    component.SlotIndex < 0 || component.SlotIndex >= ShipLoadoutRules.ComponentSlots || !slots.Add(component.SlotIndex))
                    yield return "InvalidInstalledComponent";
            slots.Clear();
            foreach (var weapon in ship.InstalledWeapons)
                if (!ValidEquipment(weapon) || weapon.IsComponent || !instances.Add(weapon.InstanceId) ||
                    weapon.SlotIndex < 0 || weapon.SlotIndex >= ShipLoadoutRules.WeaponSlots || !slots.Add(weapon.SlotIndex) ||
                    ship.EquippedWeaponIds[weapon.SlotIndex] != weapon.WeaponConfigId)
                    yield return "InvalidInstalledWeapon";
            for (int index = 0; index < ship.EquippedWeaponIds.Count; index++)
                if (!string.IsNullOrEmpty(ship.EquippedWeaponIds[index]) && !slots.Contains(index))
                    yield return "InvalidInstalledWeapon";
        }
        foreach (var market in state.Markets ?? new List<MarketRuntimeData>())
        {
            if (market == null || string.IsNullOrEmpty(market.SystemId) || string.IsNullOrEmpty(market.PlanetId))
            { yield return "InvalidMarketIdentity"; continue; }
            if (!markets.Add(market.SystemId + "\n" + market.PlanetId)) yield return "DuplicateMarketIdentity";
            if (!Finite(market.ElapsedSeconds) || market.ElapsedSeconds < 0 || !Finite(market.IntervalRemainder) || market.IntervalRemainder < 0 || market.Revision < 0)
                yield return "InvalidMarketTime";
            if (!Finite(market.LastProcessedSimulationSeconds) || market.LastProcessedSimulationSeconds < 0 ||
                market.LastProcessedSimulationSeconds > (state.Meta?.MarketSimulationSeconds ?? 0) + 1e-7 ||
                !Finite(market.EquipmentPendingSeconds) || market.EquipmentPendingSeconds < 0 || market.EquipmentPendingSeconds > market.ElapsedSeconds + 1e-7)
                yield return "InvalidMarketDeferredTime";
            var goods = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in market.Items ?? new List<MarketRuntimeItemState>())
                if (item == null || string.IsNullOrEmpty(item.ItemId) || !goods.Add(item.ItemId) || item.Quantity < 0 ||
                    !Finite(item.FractionalStock) || item.FractionalStock < 0 || item.FractionalStock >= 1)
                    yield return "InvalidMarketStock";
            foreach (var stored in market.PersonalEquipment ?? new List<SystemNpcWeaponRuntimeState>())
                if (!ValidEquipment(stored) || !instances.Add(stored.InstanceId))
                    yield return "InvalidPersonalStoredEquipment";
            foreach (var hull in market.PersonalHulls ?? new List<HullEquipmentRuntimeState>())
                if (!ValidHull(hull) || !instances.Add(hull.InstanceId))
                    yield return "InvalidStoredHull";
            foreach (var listing in market.Hulls ?? new List<MarketHullListing>())
                if (!ValidHull(listing?.Hull) ||
                    !instances.Add(listing.Hull.InstanceId) || listing.NewPrice <= 0 || !Finite(listing.ExpiresAtSeconds) || listing.ExpiresAtSeconds < 0)
                    yield return "InvalidMarketHull";
            var storedGoods = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stored in market.PersonalStorage?.entries ?? new List<RuntimeCargoEntryData>())
                if (stored == null || string.IsNullOrEmpty(stored.ItemId) || stored.Quantity <= 0 || !string.IsNullOrEmpty(stored.MissionRuntimeId) || !storedGoods.Add(stored.ItemId))
                    yield return "InvalidPersonalStoredGoods";
            foreach (var listing in market.Equipment ?? new List<MarketEquipmentListing>())
                if (!ValidEquipment(listing?.Equipment) ||
                    !instances.Add(listing.Equipment.InstanceId) || listing.NewPrice <= 0 || !Finite(listing.ExpiresAtSeconds) || listing.ExpiresAtSeconds < 0)
                    yield return "InvalidMarketEquipment";
            foreach (var receipt in market.Receipts ?? new List<MarketTransactionReceipt>())
                if (receipt == null || string.IsNullOrEmpty(receipt.OperationId) || string.IsNullOrEmpty(receipt.Intent) ||
                    !operations.Add(receipt.OperationId) || receipt.TotalPrice < 0 ||
                    (receipt.TotalPrice == 0 && !receipt.Intent.StartsWith("transfer:", StringComparison.Ordinal)) || receipt.Quantity <= 0)
                    yield return "InvalidMarketReceipt";
            var timers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var timer in market.EquipmentSpawns ?? new List<MarketEquipmentSpawnState>())
                if (timer == null || string.IsNullOrEmpty(timer.WeaponId) || !timers.Add(timer.WeaponId) ||
                    !Finite(timer.Remainder) || timer.Remainder < 0 || timer.SpawnSequence < 0)
                    yield return "InvalidMarketSpawnTimer";
        }
    }
    private static bool ValidHull(HullEquipmentRuntimeState hull) => hull != null && !string.IsNullOrEmpty(hull.InstanceId) &&
        !string.IsNullOrEmpty(hull.ConfigId) &&
        ValidCondition(hull.CurrentDurability, hull.MaximumDurability);
    private static bool ValidEquipment(SystemNpcWeaponRuntimeState item) => item != null &&
        !string.IsNullOrEmpty(item.InstanceId) && !string.IsNullOrEmpty(item.EquipmentConfigId) &&
        (!item.IsComponent || string.IsNullOrEmpty(item.WeaponConfigId)) &&
        item.StoredFuel >= 0 && (item.IsComponent || item.StoredFuel == 0) &&
        ValidCondition(item.CurrentDurability, item.MaximumDurability);
    private static bool ValidCondition(int current, int maximum) => maximum > 0 && current >= 0 && current <= maximum;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
