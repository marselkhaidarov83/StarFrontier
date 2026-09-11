using System.Collections.Generic;
using System.Linq;

public sealed class PlayerAttackService : CustomService, IPlayerAttackService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly ISystemNpcCombatService _combatService;
    private readonly ISystemTravelService _travelService;
    private readonly SimpleEventBus _eventBus;

    private readonly Dictionary<string, int> _lastShotTickByWeapon = new();
    private readonly Dictionary<int, string> _weaponTargetNpcIdsBySlot = new();

    private int _prevTick = -1;
    private int _nextAssignmentSlotIndex;

    public string CurrentTargetNpcId { get; private set; }

    public IReadOnlyDictionary<int, string> WeaponTargetNpcIdsBySlot =>
        _weaponTargetNpcIdsBySlot;

    public PlayerAttackService()
    {
        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _npcRuntimeService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();

        _combatService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcCombatService>();

        _travelService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _eventBus.Subscribe<GameTickStartedEvent>(OnGameTickStarted);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Subscribe<SystemEncounterResolvedEvent>(OnEncounterResolved);
        _eventBus.Subscribe<SystemEncounterDefeatedEvent>(OnEncounterDefeated);
        _eventBus.Subscribe<PlayerDestroyedEvent>(OnPlayerDestroyed);
    }

    private void OnGameTickStarted(GameTickStartedEvent evt)
    {
        Tick(evt.CurrentTick);
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        ClearNpcTarget(evt.RuntimeNpcId);
    }

    private void OnEncounterResolved(SystemEncounterResolvedEvent evt)
    {
        ClearAllCombatUiTargets();
    }

    private void OnEncounterDefeated(SystemEncounterDefeatedEvent evt)
    {
        ClearAllCombatUiTargets();
    }

    private void OnPlayerDestroyed(PlayerDestroyedEvent evt)
    {
        ClearAllCombatUiTargets();
    }

    private void ClearNpcTarget(string runtimeNpcId)
    {
        bool changed = false;

        if (string.Equals(
                CurrentTargetNpcId,
                runtimeNpcId,
                System.StringComparison.Ordinal))
        {
            CurrentTargetNpcId = null;
            changed = true;
        }

        List<int> slotsToClear = null;

        foreach (var pair in _weaponTargetNpcIdsBySlot)
        {
            if (string.Equals(
                    pair.Value,
                    runtimeNpcId,
                    System.StringComparison.Ordinal))
            {
                if (slotsToClear == null)
                    slotsToClear = new List<int>();

                slotsToClear.Add(pair.Key);
            }
        }

        if (slotsToClear != null)
        {
            for (int i = 0; i < slotsToClear.Count; i++)
                _weaponTargetNpcIdsBySlot.Remove(slotsToClear[i]);

            changed = true;
        }

        if (_weaponTargetNpcIdsBySlot.Count == 0)
            _nextAssignmentSlotIndex = 0;

        if (changed)
            PublishAssignmentsChanged();
    }

    public void SetTarget(string targetNpcId)
    {
        if (!IsValidHostileTarget(targetNpcId))
            return;

        CurrentTargetNpcId = targetNpcId;
        _travelService?.SetNpcDestination(targetNpcId);
        PublishAssignmentsChanged();
    }

    public void AssignNextWeaponSlotToTarget(string targetNpcId)
    {
        if (!IsValidHostileTarget(targetNpcId))
            return;

        ShipRuntimeData activeShip = GetActiveShip();

        CurrentTargetNpcId = targetNpcId;
        _travelService?.SetNpcDestination(targetNpcId);

        if (activeShip == null ||
            activeShip.EquippedWeaponIds == null ||
            activeShip.EquippedWeaponIds.Count == 0)
        {
            PublishAssignmentsChanged();
            return;
        }

        int weaponCount = activeShip.EquippedWeaponIds.Count;

        int assignedSlotIndex =
            ClampSlotIndex(_nextAssignmentSlotIndex, weaponCount);

        AssignWeaponSlotToTarget(assignedSlotIndex, targetNpcId);

        _nextAssignmentSlotIndex =
            (assignedSlotIndex + 1) % weaponCount;
    }

    public void AssignWeaponSlotToTarget(
        int weaponSlotIndex,
        string targetNpcId)
    {
        if (!IsValidHostileTarget(targetNpcId))
            return;

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null ||
            activeShip.EquippedWeaponIds == null ||
            weaponSlotIndex < 0 ||
            weaponSlotIndex >= activeShip.EquippedWeaponIds.Count)
        {
            return;
        }

        CurrentTargetNpcId = targetNpcId;
        _travelService?.SetNpcDestination(targetNpcId);
        _weaponTargetNpcIdsBySlot[weaponSlotIndex] = targetNpcId;

        PublishAssignmentsChanged();

        LogCustom(
            "[PlayerAttackService] Weapon slot assigned. Slot: " +
            weaponSlotIndex +
            ", Target: " +
            targetNpcId);
    }

    public void ClearWeaponSlotTarget(int weaponSlotIndex)
    {
        if (!_weaponTargetNpcIdsBySlot.TryGetValue(
                weaponSlotIndex,
                out string clearedTargetNpcId))
        {
            return;
        }

        _weaponTargetNpcIdsBySlot.Remove(weaponSlotIndex);

        if (_weaponTargetNpcIdsBySlot.Count == 0)
            _nextAssignmentSlotIndex = 0;

        if (!string.IsNullOrWhiteSpace(CurrentTargetNpcId) &&
            string.Equals(
                CurrentTargetNpcId,
                clearedTargetNpcId,
                System.StringComparison.Ordinal) &&
            !HasAssignedWeaponForTarget(CurrentTargetNpcId))
        {
            CurrentTargetNpcId = null;
        }

        PublishAssignmentsChanged();

        LogCustom(
            "[PlayerAttackService] Weapon slot target cleared. Slot: " +
            weaponSlotIndex);
    }

    public void ClearSelectedTargetIfNoAssignedWeapons()
    {
        if (string.IsNullOrWhiteSpace(CurrentTargetNpcId))
            return;

        if (HasAssignedWeaponForTarget(CurrentTargetNpcId))
            return;

        CurrentTargetNpcId = null;
        PublishAssignmentsChanged();
    }

    private bool HasAssignedWeaponForTarget(string targetNpcId)
    {
        if (string.IsNullOrWhiteSpace(targetNpcId))
            return false;

        foreach (var pair in _weaponTargetNpcIdsBySlot)
        {
            if (string.Equals(
                    pair.Value,
                    targetNpcId,
                    System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void ClearTarget()
    {
        ClearAllCombatUiTargets();
    }

    public void ClearAllCombatUiTargets()
    {
        CurrentTargetNpcId = null;
        _weaponTargetNpcIdsBySlot.Clear();
        _nextAssignmentSlotIndex = 0;

        _eventBus.Publish(
            CombatWeaponTargetAssignmentsChangedEvent2A.Cleared());
    }

    public void Tick(int quantTick)
    {
        if (_prevTick == quantTick)
            return;

        _prevTick = quantTick;

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null)
            return;

        if (activeShip.CurrentHull <= 0)
            return;

        if (activeShip.EquippedWeaponIds == null ||
            activeShip.EquippedWeaponIds.Count == 0)
        {
            return;
        }

        RemoveInvalidAssignments(activeShip.EquippedWeaponIds.Count);

        for (int slotIndex = 0;
             slotIndex < activeShip.EquippedWeaponIds.Count;
             slotIndex++)
        {
            string weaponConfigId =
                activeShip.EquippedWeaponIds[slotIndex];

            if (string.IsNullOrWhiteSpace(weaponConfigId))
                continue;

            if (!_weaponTargetNpcIdsBySlot.TryGetValue(
                    slotIndex,
                    out string targetNpcId))
            {
                continue;
            }

            if (!IsValidHostileTarget(targetNpcId))
            {
                _weaponTargetNpcIdsBySlot.Remove(slotIndex);
                PublishAssignmentsChanged();
                continue;
            }

            if (_lastShotTickByWeapon.TryGetValue(
                    weaponConfigId,
                    out int lastTick))
            {
                if (lastTick == quantTick)
                    continue;
            }

            bool fired =
                _combatService.TryCreatePlayerProjectile(
                    targetNpcId,
                    weaponConfigId,
                    quantTick);

            if (!fired)
                continue;

            _lastShotTickByWeapon[weaponConfigId] =
                quantTick;

            LogCustom(
                "[AttackTickDebug] Player fired once. Tick: " +
                quantTick +
                ", Slot: " +
                slotIndex +
                ", Weapon: " +
                weaponConfigId +
                ", Target: " +
                targetNpcId);

            break;
        }
    }

    private void RemoveInvalidAssignments(int weaponCount)
    {
        List<int> invalidSlots = null;

        foreach (var pair in _weaponTargetNpcIdsBySlot)
        {
            if (pair.Key < 0 ||
                pair.Key >= weaponCount ||
                !IsValidHostileTarget(pair.Value))
            {
                if (invalidSlots == null)
                    invalidSlots = new List<int>();

                invalidSlots.Add(pair.Key);
            }
        }

        bool changed = false;

        if (!string.IsNullOrWhiteSpace(CurrentTargetNpcId) &&
            !IsValidHostileTarget(CurrentTargetNpcId))
        {
            CurrentTargetNpcId = null;
            changed = true;
        }

        if (invalidSlots != null)
        {
            for (int i = 0; i < invalidSlots.Count; i++)
                _weaponTargetNpcIdsBySlot.Remove(invalidSlots[i]);

            changed = true;
        }

        if (changed)
            PublishAssignmentsChanged();
    }

    private bool IsValidHostileTarget(string targetNpcId)
    {
        if (string.IsNullOrWhiteSpace(targetNpcId))
            return false;

        if (!_npcRuntimeService.TryGetNpc(
                targetNpcId,
                out SystemNpcRuntimeState npc))
        {
            return false;
        }

        return npc.IsAlive &&
               npc.IsHostileToPlayer &&
               !npc.IsOnPlanet;
    }

    private ShipRuntimeData GetActiveShip()
    {
        if (_gameSessionService?.State?.Player == null)
            return null;

        return _gameSessionService
            .State
            .Player
            .PlayerShipState
            .GetActiveShip();
    }

    private int ClampSlotIndex(int slotIndex, int weaponCount)
    {
        if (weaponCount <= 0)
            return 0;

        if (slotIndex < 0)
            return 0;

        if (slotIndex >= weaponCount)
            return 0;

        return slotIndex;
    }

    private void PublishAssignmentsChanged()
    {
        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null ||
            activeShip.EquippedWeaponIds == null)
        {
            _eventBus.Publish(
                new CombatWeaponTargetAssignmentsChangedEvent2A(
                    CurrentTargetNpcId,
                    System.Array.Empty<CombatWeaponTargetAssignment2A>()));

            return;
        }

        CombatWeaponTargetAssignment2A[] assignments =
            _weaponTargetNpcIdsBySlot
                .OrderBy(pair => pair.Key)
                .Select(pair =>
                {
                    string weaponConfigId =
                        pair.Key >= 0 &&
                        pair.Key < activeShip.EquippedWeaponIds.Count
                            ? activeShip.EquippedWeaponIds[pair.Key]
                            : string.Empty;

                    return new CombatWeaponTargetAssignment2A(
                        pair.Key,
                        weaponConfigId,
                        pair.Value);
                })
                .ToArray();

        _eventBus.Publish(
            new CombatWeaponTargetAssignmentsChangedEvent2A(
                CurrentTargetNpcId,
                assignments));
    }
}
