using System;
using System.Collections.Generic;
using System.Linq;

    public class InventoryService : IInventoryService
    {
        private readonly SimpleEventBus eventBus;
        private readonly IGameSessionService gameSessionService;
        private readonly IConfigService configService;

        public InventoryService()
        {
            gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
            configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
            eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
            // _inventoryState = inventoryState ?? throw new ArgumentNullException(nameof(inventoryState));
            // _itemConfigProvider = itemConfigProvider ?? throw new ArgumentNullException(nameof(itemConfigProvider));
            // _getMaxCargoCapacity = getMaxCargoCapacity ?? throw new ArgumentNullException(nameof(getMaxCargoCapacity));
        }

        public IReadOnlyList<RuntimeCargoEntryData> GetItems()
        {
            return gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries;
        }

        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;

            RuntimeCargoEntryData entry = 
                gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries.FirstOrDefault((x => x.ItemId == itemId));

            return entry != null ? entry.Quantity : 0;
        }

        public int GetUsedCargo()
        {
            int total = 0;

            foreach (RuntimeCargoEntryData entry in 
                    gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || entry.Quantity <= 0)
                    continue;

                ItemConfig itemConfig = configService.GetItemConfigById(entry.ItemId);
                total += itemConfig.CargoSize * entry.Quantity;
            }

            return total;
        }

        public int GetMaxCargo()
        {
            return gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().CargoCapacity;
        }

        public int GetFreeCargo()
        {
            return Math.Max(0, GetMaxCargo() - GetUsedCargo());
        }

        public bool CanAddItem(string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
                return false;

            // if (!_itemConfigProvider.TryGetItemCargoSize(itemId, out int cargoSize))
            //     return false;

            ItemConfig itemConfig = configService.GetItemConfigById(itemId);

            int requiredCargo = itemConfig.CargoSize * quantity;
            return GetUsedCargo() + requiredCargo <= GetMaxCargo();
        }

        public bool AddItem(string itemId, int quantity)
        {
            if (!CanAddItem(itemId, quantity))
                return false;

            // InventoryEntry existingEntry = _inventoryState.CargoItems.FirstOrDefault(x => x.ItemId == itemId);
            RuntimeCargoEntryData existingEntry = 
                gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries.FirstOrDefault((x => x.ItemId == itemId));

            if (existingEntry != null)
            {
                existingEntry.Quantity += quantity;
            }
            else
            {
                gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries.Add
                    (new RuntimeCargoEntryData(itemId, quantity));
                // _inventoryState.CargoItems.Add(new InventoryEntry(itemId, quantity));
            }

            PublishCargoChanged();
            return true;
        }

        public bool RemoveItem(string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
                return false;

            // InventoryEntry existingEntry = _inventoryState.CargoItems.FirstOrDefault(x => x.ItemId == itemId);
            RuntimeCargoEntryData existingEntry = 
                gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries.FirstOrDefault((x => x.ItemId == itemId));

            if (existingEntry == null)
                return false;

            if (existingEntry.Quantity < quantity)
                return false;

            existingEntry.Quantity -= quantity;

            if (existingEntry.Quantity <= 0)
            {
                gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries.Remove(existingEntry);
                // _inventoryState.CargoItems.Remove(existingEntry);
            }

            PublishCargoChanged();
            return true;
        }

        public void Clear()
        {
            gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().Cargo.entries.Clear();
            // _inventoryState.CargoItems.Clear();
            PublishCargoChanged();
        }

        private void PublishCargoChanged()
        {
            // eventBus.Publish(new CargoChangedEvent(GetUsedCargo(), GetMaxCargo()));
            eventBus.Publish(new CargoChangedEvent(GetUsedCargo()));
        }
    }