using System.Collections.Generic;

public interface IPlayerAttackService
{
    string CurrentTargetNpcId { get; }

    IReadOnlyDictionary<int, string> WeaponTargetNpcIdsBySlot { get; }

    void SetTarget(string targetNpcId);
    void AssignNextWeaponSlotToTarget(string targetNpcId);
    void AssignWeaponSlotToTarget(int weaponSlotIndex, string targetNpcId);
    void ClearWeaponSlotTarget(int weaponSlotIndex);
    void ClearSelectedTargetIfNoAssignedWeapons();
    void ClearTarget();
    void ClearAllCombatUiTargets();

    void Tick(int quantTick);
}
