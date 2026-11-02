using System;
using System.Collections.Generic;
using UnityEngine;

// Persistent world loot shared by the visible and scheduled offscreen systems.
public sealed class SystemSpaceLootService : CustomService
{
    private readonly List<SpaceLootRuntimeState> _loot = new();
    private readonly HashSet<string> _lootIds = new();
    private readonly HashSet<string> _playerCollectionMarks = new();
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IGameSessionService _gameSessionService;
    private readonly SimpleEventBus _eventBus;
    private readonly IConfigService _configService;
    private readonly IInventoryService _inventoryService;
    private readonly ISystemTravelService _travelService;
    private readonly IShipMovementService _shipMovementService;
    private readonly ITargetService2A _targetService;

    public IReadOnlyList<SpaceLootRuntimeState> Loot => _loot;

    public SystemSpaceLootService()
    {
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _inventoryService = Bootstrapper.Instance.ServiceRegistry.Get<IInventoryService>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _shipMovementService = Bootstrapper.Instance.ServiceRegistry.Get<IShipMovementService>();
        _targetService = Bootstrapper.Instance.ServiceRegistry.Get<ITargetService2A>();
    }

    public bool Add(SpaceLootRuntimeState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.SystemId) || state.Quantity <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(state.LootId))
            state.LootId = Guid.NewGuid().ToString("N");

        if (!_lootIds.Add(state.LootId))
            return false;

        if (state.CurrentHealth <= 0)
            state.CurrentHealth = Bootstrapper.Instance.SpaceLootCombatConfig != null
                ? Bootstrapper.Instance.SpaceLootCombatConfig.MaxHealth : 1;

        SpaceLootRuntimeState copy = state.Copy();
        if (copy.Equipment != null && !copy.Equipment.HasLootOrigin &&
            !string.IsNullOrWhiteSpace(copy.SourceNpcConfigId))
        {
            copy.Equipment.HasLootOrigin = true;
            copy.Equipment.LootOriginNpcType = copy.SourceNpcType;
            copy.Equipment.LootOriginNpcConfigId = copy.SourceNpcConfigId;
            AppLog.Info(DebugLogChannel.Loot,
                "[LOOT_EQUIPMENT_ORIGIN_RECORDED] Id=" + copy.LootId +
                " | Source=" + copy.SourceNpcConfigId);
        }
        copy.PullCollectorId = null; // A pursuit never owns a trophy across a reload.
        _loot.Add(copy);
        AppLog.Info(DebugLogChannel.Loot,
            "[LOOT_CREATED] Id=" + state.LootId + " | System=" + state.SystemId +
            " | Kind=" + state.Kind + " | Quantity=" + state.Quantity +
            " | SourceNpc=" + state.SourceNpcId);
        return true;
    }

    public List<SpaceLootRuntimeState> Capture()
    {
        var captured = new List<SpaceLootRuntimeState>(_loot.Count);
        for (int i = 0; i < _loot.Count; i++)
            captured.Add(_loot[i].Copy());
        return captured;
    }

    public void Restore(List<SpaceLootRuntimeState> states)
    {
        Clear();

        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
            Add(states[i]);
    }

    public void Clear()
    {
        _loot.Clear();
        _lootIds.Clear();
        _playerCollectionMarks.Clear();
    }

    public bool Remove(string lootId)
    {
        if (string.IsNullOrWhiteSpace(lootId) || !_lootIds.Contains(lootId))
            return false;

        for (int i = 0; i < _loot.Count; i++)
        {
            if (_loot[i].LootId != lootId)
                continue;

            if (_targetService.IsCurrentTarget(lootId))
            {
                _travelService.CancelTravel();
                _shipMovementService.StopImmediately();
                _targetService.ClearTarget();
            }
            _loot.RemoveAt(i);
            _lootIds.Remove(lootId);
            _playerCollectionMarks.Remove(lootId);
            AppLog.Info(DebugLogChannel.Loot, "[LOOT_REMOVED] Id=" + lootId);
            return true;
        }

        _lootIds.Remove(lootId);
        _playerCollectionMarks.Remove(lootId);
        return false;
    }

    public bool IsPlayerCollectionMarked(string lootId) =>
        !string.IsNullOrWhiteSpace(lootId) && _playerCollectionMarks.Contains(lootId);

    public SpaceLootRuntimeState Find(string lootId)
    {
        if (string.IsNullOrWhiteSpace(lootId)) return null;
        for (int i = 0; i < _loot.Count; i++)
            if (_loot[i] != null && _loot[i].LootId == lootId)
                return _loot[i];
        return null;
    }

    public int GetLootMass(SpaceLootRuntimeState loot)
    {
        if (loot == null || loot.Quantity <= 0) return 0;
        if (loot.Kind == SpaceLootKind.Credits || loot.Kind == SpaceLootKind.Diamonds)
            return 0;
        int unitMass;
        if (loot.Kind == SpaceLootKind.Equipment)
        {
            WeaponConfig weapon = _configService.GetWeaponConfigById(loot.EquipmentConfigId);
            if (weapon == null && (loot.Equipment == null ||
                loot.Equipment.RolledStats.CargoSize <= 0))
                return int.MaxValue;
            unitMass = loot.Equipment != null && loot.Equipment.RolledStats.CargoSize > 0
                ? loot.Equipment.RolledStats.CargoSize : weapon != null ? weapon.CargoSize : 0;
        }
        else
        {
            ItemConfig item = _configService.GetItemConfigById(loot.ItemConfigId);
            if (item == null) return int.MaxValue;
            unitMass = item.CargoSize;
        }
        return (int)Math.Min(int.MaxValue, (long)Math.Max(0, unitMass) * loot.Quantity);
    }

    private GrabberConfig PlayerGrabber()
    {
        ShipRuntimeData ship = _gameSessionService.State?.Player?.GetActiveShip();
        return ship == null || ship.DebugGrabberRemoved
            ? null : _configService.GetAllyConfigById(ship.AllyConfigId)?.Grabber;
    }

    public bool TrySetPlayerGrabberEnabled(bool enabled, out string error)
    {
        error = null;
        ShipRuntimeData ship = _gameSessionService.State?.Player?.GetActiveShip();
        if (ship == null)
        {
            error = "Корабль игрока недоступен";
            return false;
        }
        if (enabled && _configService.GetAllyConfigById(ship.AllyConfigId)?.Grabber == null)
        {
            error = "В конфигурации корабля нет захвата";
            return false;
        }
        ship.DebugGrabberRemoved = !enabled;
        if (!enabled)
        {
            foreach (string id in _playerCollectionMarks)
                ReleasePull(id, "player");
            _playerCollectionMarks.Clear();
        }
        AppLog.Info(DebugLogChannel.Loot,
            "[DEBUG_PLAYER_GRABBER_CHANGED] Ship=" + ship.ShipId + " | Enabled=" + enabled);
        return true;
    }

    public bool TryTogglePlayerCollectionMark(string lootId, out string error)
    {
        error = null;
        SpaceLootRuntimeState loot = Find(lootId);
        PlayerState player = _gameSessionService.State?.Player;
        if (loot == null || player == null || loot.SystemId != player.CurrentSystemId)
        {
            error = "Трофей недоступен";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                "LootUnavailable", lootId);
            return false;
        }
        if (_playerCollectionMarks.Remove(lootId))
        {
            ReleasePull(lootId, "player");
            AppLog.Info(DebugLogChannel.Loot, "[LOOT_UNMARKED] Id=" + lootId);
            return true;
        }
        GrabberConfig grabber = PlayerGrabber();
        if (grabber == null)
        {
            error = "На корабле не установлен захват. Сбор невозможен";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                "GrabberUnavailable", lootId);
            return false;
        }
        if (grabber.Strength <= 0 || grabber.Range <= 0f ||
            grabber.PullSpeedPerTick <= 0f)
        {
            error = "Захват не работает";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                "GrabberDisabled", lootId);
            return false;
        }
        if (!string.IsNullOrEmpty(loot.PullCollectorId) &&
            loot.PullCollectorId != "player")
        {
            error = "Трофей уже собирают";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                "LootClaimedByOther", lootId);
            return false;
        }
        int mass = GetLootMass(loot);
        if (mass > grabber.Strength)
        {
            string name = loot.Kind == SpaceLootKind.Equipment
                ? _configService.GetWeaponConfigById(loot.EquipmentConfigId)?.DisplayName
                : _configService.GetItemConfigById(loot.ItemConfigId)?.DisplayName;
            error = string.IsNullOrWhiteSpace(name)
                ? "Трофей\nслишком тяжёлый" : "Трофей «" + name.Trim() + "»\nслишком тяжёлый";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                "GrabberTooWeak", lootId);
            return false;
        }
        _playerCollectionMarks.Add(lootId);
        AppLog.Info(DebugLogChannel.Loot, "[LOOT_MARKED] Id=" + lootId);
        return true;
    }

    public void TickCollection(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        float tickFraction = deltaTime / Mathf.Max(0.01f, GameTimeState.SecondsPerDay);
        TickPlayerCollection(tickFraction);
        TickVisibleNpcCollection(tickFraction);
    }

    public bool TryDebugClearPlayerCargo(out string error)
    {
        error = null;
        ShipRuntimeData ship = _gameSessionService.State?.Player?.GetActiveShip();
        if (ship == null)
        {
            error = "Корабль игрока недоступен";
            return false;
        }
        ship.CargoEquipment?.Clear();
        _inventoryService.Clear();
        AppLog.Info(DebugLogChannel.Loot, "[DEBUG_PLAYER_CARGO_CLEARED] Ship=" + ship.ShipId);
        return true;
    }

    public bool TryDebugAddAstralite(out string error)
    {
        const string itemId = "item_astralite_ancients_01";
        error = null;
        if (_gameSessionService.State?.Player?.GetActiveShip() == null)
        {
            error = "Корабль игрока недоступен";
            return false;
        }
        if (!_inventoryService.AddLootItem(itemId, 5))
        {
            error = "Не удалось добавить астралит в трюм";
            return false;
        }
        if (_inventoryService.GetFreeCargo() < 0)
        {
            _travelService.CancelTravel();
            _shipMovementService.StopImmediately();
        }
        AppLog.Info(DebugLogChannel.Loot,
            "[DEBUG_PLAYER_ASTRALITE_ADDED] Item=" + itemId + " | Quantity=5");
        return true;
    }

    public bool TryDebugRemoveAstralite(out string error)
    {
        const string itemId = "item_astralite_ancients_01";
        error = null;
        if (_gameSessionService.State?.Player?.GetActiveShip() == null)
        {
            error = "Корабль игрока недоступен";
            return false;
        }
        if (!_inventoryService.RemoveItem(itemId, 5))
        {
            error = "Недостаточно астралита Древних для удаления 5 единиц";
            return false;
        }
        AppLog.Info(DebugLogChannel.Loot,
            "[DEBUG_PLAYER_ASTRALITE_REMOVED] Item=" + itemId + " | Quantity=5");
        return true;
    }

    public bool TryDebugAddEnemyEquipment(out string error) => TryDebugAddEquipmentSamples(
        new[] { "weapon_ai_laser_silver_L01_01", "weapon_ancient_lance_silver_L01_01",
            "weapon_infected_acid_silver_L01_01" },
        SystemNpcType.Enemy,
        new[] { "enemy_ai_L01_01", "enemy_ancients_L01_01", "enemy_infected_L01_01" }, out error);

    public bool TryDebugAddNpcEquipment(out string error) => TryDebugAddEquipmentSamples(
        new[] { "weapon_common_laser_silver_L01_01" }, SystemNpcType.Ally,
        new[] { "ally_ranger_L00_01" }, out error);

    public bool TryDebugAddEveryAstralite(out string error) => TryDebugAddItemSamples(
        new[] { "item_astralite_ai_01", "item_astralite_ancients_01", "item_astralite_infected_01" }, out error);

    public bool TryDebugAddCommodity(out string error) => TryDebugAddItemSamples(new[] { "item_ore_01" }, out error);

    private bool TryDebugAddEquipmentSamples(string[] weaponIds, SystemNpcType sourceType,
        string[] sourceIds, out string error)
    {
        error = null;
        ShipRuntimeData ship = _gameSessionService.State?.Player?.GetActiveShip();
        if (ship == null) { error = "Корабль игрока недоступен"; return false; }
        var configs = new WeaponConfig[weaponIds.Length];
        for (int i = 0; i < configs.Length; i++)
        {
            configs[i] = _configService.GetWeaponConfigById(weaponIds[i]);
            if (configs[i] == null) { error = "Конфигурация тестового оборудования не найдена"; return false; }
        }
        ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
        for (int i = 0; i < configs.Length; i++)
        {
            WeaponRuntimeStats stats = configs[i].RollRuntimeStats();
            ship.CargoEquipment.Add(new SystemNpcWeaponRuntimeState
            {
                InstanceId = Guid.NewGuid().ToString("N"), WeaponConfigId = configs[i].Id,
                RolledStats = stats, ShotDistance = stats.Range, DurabilityPercent = 100,
                HasLootOrigin = true, LootOriginNpcType = sourceType, LootOriginNpcConfigId = sourceIds[i]
            });
        }
        StopDebugOverloadedShip();
        _eventBus.Publish(new CargoChangedEvent(_inventoryService.GetUsedCargo()));
        AppLog.Info(DebugLogChannel.Loot,
            "[DEBUG_EQUIPMENT_SAMPLES_ADDED] Source=" + sourceType + " | Count=" + configs.Length);
        return true;
    }

    private bool TryDebugAddItemSamples(string[] itemIds, out string error)
    {
        error = null;
        ShipRuntimeData ship = _gameSessionService.State?.Player?.GetActiveShip();
        if (ship?.Cargo == null) { error = "Трюм игрока недоступен"; return false; }
        foreach (string id in itemIds)
            if (_configService.GetItemConfigById(id) == null || ship.Cargo.GetTradeableQuantity(id) == int.MaxValue)
            { error = "Не удалось подготовить тестовые товары"; return false; }
        foreach (string id in itemIds) ship.Cargo.AddLootItem(id, 1);
        StopDebugOverloadedShip();
        _eventBus.Publish(new CargoChangedEvent(_inventoryService.GetUsedCargo()));
        AppLog.Info(DebugLogChannel.Loot, "[DEBUG_ITEM_SAMPLES_ADDED] Items=" + string.Join(",", itemIds));
        return true;
    }

    private void StopDebugOverloadedShip()
    {
        if (_inventoryService.GetFreeCargo() >= 0) return;
        _travelService.CancelTravel();
        _shipMovementService.StopImmediately();
    }

    private void TickPlayerCollection(float tickFraction)
    {
        PlayerState player = _gameSessionService.State?.Player;
        GrabberConfig grabber = PlayerGrabber();
        if (player == null || player.IsOnPlanet() || grabber == null)
        {
            foreach (string id in _playerCollectionMarks)
                ReleasePull(id, "player");
            _playerCollectionMarks.Clear();
            return;
        }
        var marked = new List<string>(_playerCollectionMarks);
        SpaceLootRuntimeState nearest = null;
        SpaceLootRuntimeState active = null;
        float nearestDistance = float.MaxValue;
        float activeDistance = float.MaxValue;
        foreach (string id in marked)
        {
            SpaceLootRuntimeState loot = Find(id);
            if (loot == null || loot.SystemId != player.CurrentSystemId)
            {
                ReleasePull(id, "player");
                _playerCollectionMarks.Remove(id);
                continue;
            }
            if (!string.IsNullOrEmpty(loot.PullCollectorId) && loot.PullCollectorId != "player")
                continue;
            float distance = Vector3.Distance(loot.Position, player.SystemMapShipPosition);
            if (distance > grabber.Range || GetLootMass(loot) > grabber.Strength)
            {
                ReleasePull(id, "player");
                continue;
            }
            if (distance < nearestDistance ||
                (distance == nearestDistance && nearest != null &&
                 string.CompareOrdinal(loot.LootId, nearest.LootId) < 0))
            {
                nearest = loot;
                nearestDistance = distance;
            }
            if (loot.PullCollectorId == "player" && distance < activeDistance)
            {
                active = loot;
                activeDistance = distance;
            }
        }
        // Finish the current pull before choosing the next nearest marked trophy.
        SpaceLootRuntimeState selected = active ?? nearest;
        foreach (string id in marked)
            if (selected == null || id != selected.LootId)
                ReleasePull(id, "player");
        if (selected == null)
            return;
        if (selected.PullCollectorId != "player")
            AppLog.Info(DebugLogChannel.Loot,
                "[LOOT_PULL_STARTED] Id=" + selected.LootId + " | Collector=player");
        selected.PullCollectorId = "player";
        selected.EjectionRemainingSeconds = 0f;
        _travelService.CancelTravel();
        _shipMovementService.StopImmediately();
        selected.Position = Vector3.MoveTowards(selected.Position, player.SystemMapShipPosition,
            grabber.PullSpeedPerTick * tickFraction);
        if (Vector3.Distance(selected.Position, player.SystemMapShipPosition) <= 0.01f)
            TryCollectForPlayer(selected.LootId, out _);
    }

    private void TickVisibleNpcCollection(float tickFraction)
    {
        string systemId = _gameSessionService.State?.Player?.CurrentSystemId;
        // Collection removes entries, so walk backwards.
        for (int i = _loot.Count - 1; i >= 0; i--)
        {
            SpaceLootRuntimeState loot = _loot[i];
            if (loot.SystemId != systemId || string.IsNullOrEmpty(loot.PullCollectorId) ||
                loot.PullCollectorId == "player")
                continue;
            string collectorId = loot.PullCollectorId;
            if (!_npcRuntimeService.TryGetNpc(collectorId, out SystemNpcRuntimeState npc) ||
                npc == null || !npc.IsAlive || npc.IsOnPlanet ||
                npc.CurrentSystemId != systemId ||
                npc.CurrentBehavior != SystemNpcBehaviorType.CollectDebris ||
                npc.LootTargetId != loot.LootId)
            {
                ReleasePull(loot.LootId, collectorId);
                continue;
            }
            GrabberConfig grabber = _configService.GetAllyConfigById(npc.ConfigId)?.Grabber;
            if (grabber == null || grabber.Range <= 0f || grabber.PullSpeedPerTick <= 0f ||
                Vector3.Distance(loot.Position, npc.CurrentPosition) > grabber.Range ||
                !RangerLootCollectionRules2A.CanCollect(npc, loot, GetLootMass(loot),
                    grabber.Strength, RangerLootCollectionRules2A.GetFreeCargoMass(npc, _configService)))
            {
                ReleasePull(loot.LootId, collectorId);
                continue;
            }
            loot.EjectionRemainingSeconds = 0f;
            loot.Position = Vector3.MoveTowards(loot.Position, npc.CurrentPosition,
                grabber.PullSpeedPerTick * tickFraction);
            if (Vector3.Distance(loot.Position, npc.CurrentPosition) <= 0.01f)
                TryCollectForNpc(loot.LootId, npc, out _);
        }
    }

    public bool TryPullTowardNpc(string lootId, SystemNpcRuntimeState npc,
        float speedPerTick, out bool arrived)
    {
        arrived = false;
        SpaceLootRuntimeState loot = Find(lootId);
        if (npc == null || loot == null || npc.CurrentSystemId != loot.SystemId ||
            (!string.IsNullOrEmpty(loot.PullCollectorId) &&
             loot.PullCollectorId != npc.RuntimeNpcId))
            return false;
        if (loot.PullCollectorId != npc.RuntimeNpcId)
            AppLog.Info(DebugLogChannel.Loot,
                "[LOOT_PULL_STARTED] Id=" + lootId + " | Collector=" + npc.RuntimeNpcId);
        loot.PullCollectorId = npc.RuntimeNpcId;
        loot.EjectionRemainingSeconds = 0f;
        if (loot.SystemId == _gameSessionService.State?.Player?.CurrentSystemId)
        {
            // Visible pulls advance with frame time in TickCollection.
            arrived = Vector3.Distance(loot.Position, npc.CurrentPosition) <= 0.01f;
            return true;
        }
        loot.Position = Vector3.MoveTowards(loot.Position, npc.CurrentPosition,
            Math.Max(0f, speedPerTick));
        arrived = Vector3.Distance(loot.Position, npc.CurrentPosition) <= 0.01f;
        return true;
    }

    public void ReleasePull(string lootId, string collectorId)
    {
        SpaceLootRuntimeState loot = Find(lootId);
        if (loot != null && loot.PullCollectorId == collectorId)
            loot.PullCollectorId = null;
    }

    public bool TryCollectForPlayer(string lootId, out string error)
    {
        error = null;
        SpaceLootRuntimeState loot = Find(lootId);
        PlayerState player = _gameSessionService.State?.Player;
        if (loot == null || player == null || player.CurrentSystemId != loot.SystemId ||
            (loot.PullCollectorId != null && loot.PullCollectorId != "player"))
        {
            error = "Трофей недоступен";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                "LootUnavailable", lootId);
            return false;
        }
        GrabberConfig grabber = PlayerGrabber();
        if (grabber == null || grabber.Range <= 0f ||
            grabber.PullSpeedPerTick <= 0f || GetLootMass(loot) > grabber.Strength ||
            Vector3.Distance(loot.Position, player.SystemMapShipPosition) > grabber.Range)
        {
            error = "Захват не достаёт до трофея";
            string reason = grabber == null || grabber.Range <= 0f || grabber.PullSpeedPerTick <= 0f
                ? "GrabberUnavailable"
                : GetLootMass(loot) > grabber.Strength ? "GrabberTooWeak" : "GrabberOutOfRange";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                reason, lootId);
            return false;
        }
        if (loot.Kind == SpaceLootKind.Credits)
        {
            if (player.Credits > int.MaxValue - loot.Quantity)
            {
                error = "Кошелёк полон";
                PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                    "CreditsOverflow", lootId);
                return false;
            }
            Bootstrapper.Instance.ServiceRegistry.Get<IEconomyService>().AddCredits(loot.Quantity);
        }
        else if (loot.Kind == SpaceLootKind.Diamonds)
        {
            if (player.Diamonds > int.MaxValue - loot.Quantity)
            {
                error = "Кошелёк полон";
                PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                    "DiamondsOverflow", lootId);
                return false;
            }
            player.Diamonds += loot.Quantity;
            _eventBus.Publish(new DiamondsChangedEvent(player.Diamonds));
        }
        else if (loot.Kind == SpaceLootKind.Equipment)
        {
            if (loot.Equipment == null)
            {
                error = "Оборудование повреждено";
                PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                    "EquipmentMissing", lootId);
                return false;
            }
            ShipRuntimeData ship = player.GetActiveShip();
            if (ship == null)
            {
                error = "Трюм недоступен";
                PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                    "CargoUnavailable", lootId);
                return false;
            }
            ship.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
            ship.CargoEquipment.Add(loot.Equipment);
            _eventBus.Publish(new CargoChangedEvent(_inventoryService.GetUsedCargo()));
        }
        else if (!_inventoryService.AddLootItem(loot.ItemConfigId, loot.Quantity))
        {
            error = "Не удалось поместить трофей в трюм";
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, false,
                "CargoRejected", lootId);
            return false;
        }
        Remove(lootId);
        if (_inventoryService.GetFreeCargo() < 0)
        {
            _travelService.CancelTravel();
            _shipMovementService.StopImmediately();
        }
        AppLog.Info(DebugLogChannel.Loot, "[LOOT_COLLECTED_PLAYER] Id=" + lootId);
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.LootCollect, true,
            contentId: lootId, quantity: loot.Quantity);
        return true;
    }

    public bool TryCollectForNpc(string lootId, SystemNpcRuntimeState npc, out string error)
    {
        error = null;
        SpaceLootRuntimeState loot = Find(lootId);
        if (npc == null || loot == null || npc.CurrentSystemId != loot.SystemId ||
            (loot.PullCollectorId != null && loot.PullCollectorId != npc.RuntimeNpcId))
        {
            error = "Трофей недоступен";
            return false;
        }
        AllyConfig ally = _configService.GetAllyConfigById(npc.ConfigId);
        if (ally?.Grabber == null || ally.Grabber.Range <= 0f ||
            ally.Grabber.PullSpeedPerTick <= 0f ||
            GetLootMass(loot) > ally.Grabber.Strength ||
            RangerLootCollectionRules2A.GetFreeCargoMass(npc, _configService) < GetLootMass(loot) ||
            Vector3.Distance(loot.Position, npc.CurrentPosition) > ally.Grabber.Range)
        {
            error = "Захват или трюм не позволяет собрать трофей";
            return false;
        }
        if (loot.Kind == SpaceLootKind.Equipment)
        {
            if (loot.Equipment == null) return false;
            npc.CargoEquipment ??= new List<SystemNpcWeaponRuntimeState>();
            npc.CargoEquipment.Add(loot.Equipment);
        }
        else if (loot.Kind != SpaceLootKind.Credits && loot.Kind != SpaceLootKind.Diamonds)
        {
            npc.CargoBatches ??= new List<NpcCargoBatchRuntimeState>();
            npc.CargoBatches.Add(new NpcCargoBatchRuntimeState
            {
                BatchId = string.IsNullOrWhiteSpace(loot.SourceBatchId)
                    ? Guid.NewGuid().ToString("N") : loot.SourceBatchId,
                ItemConfigId = loot.ItemConfigId,
                Quantity = loot.Quantity,
                BatchNumber = npc.CargoBatches.Count + 1
            });
        }
        Remove(lootId);
        AppLog.Info(DebugLogChannel.Loot, "[LOOT_COLLECTED_NPC] Id=" + lootId +
            " | Npc=" + npc.RuntimeNpcId);
        return true;
    }

    public bool TryDropPlayerCargo(string itemId, int quantity, out string error)
    {
        error = null;
        PlayerState player = _gameSessionService.State?.Player;
        ShipRuntimeData ship = player?.GetActiveShip();
        if (player == null || player.IsOnPlanet() || ship?.Cargo == null ||
            string.IsNullOrWhiteSpace(itemId) || quantity <= 0 ||
            ship.Cargo.GetQuantity(itemId) < quantity)
        {
            error = "Выберите доступное количество товара";
            return false;
        }
        if (_configService.GetItemConfigById(itemId) == null)
        {
            error = "Товар не найден";
            return false;
        }
        SpaceLootRuntimeState drop = NewPlayerDrop(player, SpaceLootKind.Item,
            itemId, null, null, quantity);
        if (!Add(drop) || !ship.Cargo.RemoveAnyItem(itemId, quantity))
        {
            Remove(drop.LootId);
            error = "Не удалось выбросить товар";
            return false;
        }
        _eventBus.Publish(new CargoChangedEvent(_inventoryService.GetUsedCargo()));
        AppLog.Info(DebugLogChannel.Loot, "[LOOT_DROPPED_PLAYER] Id=" + drop.LootId +
            " | Item=" + itemId + " | Quantity=" + quantity);
        return true;
    }

    public bool TryDropPlayerEquipment(string instanceId, out string error)
    {
        error = null;
        PlayerState player = _gameSessionService.State?.Player;
        ShipRuntimeData ship = player?.GetActiveShip();
        if (player == null || player.IsOnPlanet() || ship?.CargoEquipment == null)
        {
            error = "Трюм недоступен";
            return false;
        }
        SystemNpcWeaponRuntimeState equipment = null;
        for (int i = 0; i < ship.CargoEquipment.Count; i++)
            if (ship.CargoEquipment[i]?.InstanceId == instanceId)
            {
                equipment = ship.CargoEquipment[i];
                break;
            }
        if (equipment == null)
        {
            error = "Оборудование не найдено";
            return false;
        }
        SpaceLootRuntimeState drop = NewPlayerDrop(player, SpaceLootKind.Equipment,
            null, equipment.WeaponConfigId, equipment, 1);
        if (!Add(drop) || !ship.CargoEquipment.Remove(equipment))
        {
            Remove(drop.LootId);
            error = "Не удалось выбросить оборудование";
            return false;
        }
        _eventBus.Publish(new CargoChangedEvent(_inventoryService.GetUsedCargo()));
        AppLog.Info(DebugLogChannel.Loot, "[LOOT_DROPPED_PLAYER] Id=" + drop.LootId +
            " | Equipment=" + equipment.InstanceId);
        return true;
    }

    private static SpaceLootRuntimeState NewPlayerDrop(PlayerState player,
        SpaceLootKind kind, string itemId, string equipmentId,
        SystemNpcWeaponRuntimeState equipment, int quantity)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float distance = Bootstrapper.Instance.SpaceLootEjectionDistance;
        Vector3 position = player.SystemMapShipPosition;
        return new SpaceLootRuntimeState
        {
            LootId = Guid.NewGuid().ToString("N"),
            SourceNpcId = player.PlayerId,
            SystemId = player.CurrentSystemId,
            Position = position,
            EjectionTargetPosition = NpcLootProcessor.ComputeEjectionPosition(
                position, distance, angle),
            EjectionRemainingSeconds = 0.6f,
            Kind = kind,
            ItemConfigId = itemId,
            EquipmentConfigId = equipmentId,
            Equipment = equipment,
            Quantity = quantity
        };
    }

    public bool ApplyDamage(string lootId, int damage)
    {
        if (damage <= 0 || string.IsNullOrWhiteSpace(lootId))
            return false;

        for (int i = 0; i < _loot.Count; i++)
        {
            SpaceLootRuntimeState drop = _loot[i];
            if (drop == null || drop.LootId != lootId)
                continue;

            drop.CurrentHealth = Mathf.Max(0, drop.CurrentHealth - damage);
            if (drop.CurrentHealth > 0)
                return true;

            var destroyed = new SpaceLootDestroyedEvent(drop);
            Remove(lootId);
            _eventBus.Publish(destroyed);
            return true;
        }

        return false;
    }

    public void TickEjection(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        for (int i = 0; i < _loot.Count; i++)
        {
            SpaceLootRuntimeState drop = _loot[i];
            if (drop == null || drop.EjectionRemainingSeconds <= 0f ||
                !string.IsNullOrEmpty(drop.PullCollectorId))
                continue;

            float remaining = drop.EjectionRemainingSeconds;
            float step = Mathf.Min(deltaTime, remaining);
            drop.Position = Vector3.Lerp(drop.Position,
                drop.EjectionTargetPosition, step / remaining);
            drop.EjectionRemainingSeconds = Mathf.Max(0f, remaining - step);
            if (drop.EjectionRemainingSeconds <= 0f)
                drop.Position = drop.EjectionTargetPosition;
        }
    }

    public void CleanupSystem(string systemId, int quantTick)
    {
        if (string.IsNullOrWhiteSpace(systemId) || _loot.Count == 0)
            return;

        IReadOnlyList<SystemNpcRuntimeState> npcs = _npcRuntimeService.Npcs;
        if (npcs == null || npcs.Count == 0)
            return;

        var eligibleNpcs = new List<SystemNpcRuntimeState>();
        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];
            if (npc != null && npc.IsAvailableForCombat() && !npc.IsFighting &&
                (npc.IsAlly || npc.IsEnemy) && npc.CurrentSystemId == systemId &&
                npc.Weapons != null)
                eligibleNpcs.Add(npc);
        }

        if (eligibleNpcs.Count == 0)
            return;

        for (int lootIndex = _loot.Count - 1; lootIndex >= 0; lootIndex--)
        {
            SpaceLootRuntimeState drop = _loot[lootIndex];
            if (drop != null && !string.IsNullOrEmpty(drop.PullCollectorId))
            {
                bool collectorStillPursues = drop.PullCollectorId == "player"
                    ? _playerCollectionMarks.Contains(drop.LootId)
                    : false;
                if (!collectorStillPursues)
                    for (int i = 0; i < npcs.Count; i++)
                    {
                        SystemNpcRuntimeState collector = npcs[i];
                        if (collector != null && collector.IsAlive &&
                            collector.RuntimeNpcId == drop.PullCollectorId &&
                            collector.LootTargetId == drop.LootId)
                        {
                            collectorStillPursues = true;
                            break;
                        }
                    }
                if (!collectorStillPursues)
                    drop.PullCollectorId = null;
            }
            if (drop == null || drop.SystemId != systemId || IsPlayerNearby(drop) ||
                !string.IsNullOrEmpty(drop.PullCollectorId))
                continue;

            for (int npcIndex = 0; npcIndex < eligibleNpcs.Count; npcIndex++)
            {
                SystemNpcRuntimeState npc = eligibleNpcs[npcIndex];
                if (npc.LootTargetId == drop.LootId)
                    continue;

                float distance = Vector3.Distance(npc.CurrentPosition, drop.Position);
                for (int weaponIndex = 0; weaponIndex < npc.Weapons.Count; weaponIndex++)
                {
                    SystemNpcWeaponRuntimeState weapon = npc.Weapons[weaponIndex];
                    if (weapon == null || weapon.ShotDistance <= 0f ||
                        distance > weapon.ShotDistance || !weapon.CanShootAtTick(quantTick))
                        continue;

                    if (_configService.GetWeaponConfigById(weapon.WeaponConfigId)?.ShotType == WeaponShotType2A.Wave)
                        continue;

                    weapon.MarkShotAtTick(quantTick, 1);
                    ApplyDamage(drop.LootId, Mathf.Max(1, drop.CurrentHealth));
                    AppLog.Info(DebugLogChannel.Loot,
                        "[LOOT_DESTROYED_BY_NPC] Id=" + drop.LootId +
                        " | Npc=" + npc.RuntimeNpcId +
                        " | NpcType=" + npc.NpcType +
                        " | ConfigId=" + npc.ConfigId +
                        " | Name=" + npc.DisplayName +
                        " | Weapon=" + weapon.WeaponConfigId +
                        " | System=" + systemId +
                        " | Offscreen=" + (_gameSessionService.State?.Player?.CurrentSystemId != systemId) +
                        " | Tick=" + quantTick);
                    npcIndex = eligibleNpcs.Count;
                    break;
                }
            }
        }
    }

    private bool IsPlayerNearby(SpaceLootRuntimeState drop)
    {
        PlayerState player = _gameSessionService.State != null
            ? _gameSessionService.State.Player
            : null;

        if (player == null || player.IsOnPlanet() ||
            player.CurrentSystemId != drop.SystemId)
            return false;

        float radius = Bootstrapper.Instance.SpaceLootPlayerSafeRadius;
        return Vector3.SqrMagnitude(player.SystemMapShipPosition - drop.Position) <=
               radius * radius;
    }
}
