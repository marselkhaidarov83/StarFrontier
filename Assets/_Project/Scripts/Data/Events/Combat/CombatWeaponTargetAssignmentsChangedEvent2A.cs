using System;

[Serializable]
public readonly struct CombatWeaponTargetAssignment2A
{
    public readonly int WeaponSlotIndex;
    public readonly string WeaponConfigId;
    public readonly string TargetNpcId;

    public CombatWeaponTargetAssignment2A(
        int weaponSlotIndex,
        string weaponConfigId,
        string targetNpcId)
    {
        WeaponSlotIndex = weaponSlotIndex;
        WeaponConfigId = weaponConfigId ?? string.Empty;
        TargetNpcId = targetNpcId ?? string.Empty;
    }
}

public readonly struct CombatWeaponTargetAssignmentsChangedEvent2A
{
    public readonly string SelectedTargetNpcId;
    public readonly CombatWeaponTargetAssignment2A[] Assignments;

    public bool HasSelectedTarget =>
        !string.IsNullOrWhiteSpace(SelectedTargetNpcId);

    public CombatWeaponTargetAssignmentsChangedEvent2A(
        string selectedTargetNpcId,
        CombatWeaponTargetAssignment2A[] assignments)
    {
        SelectedTargetNpcId = selectedTargetNpcId ?? string.Empty;
        Assignments = assignments ?? Array.Empty<CombatWeaponTargetAssignment2A>();
    }

    public static CombatWeaponTargetAssignmentsChangedEvent2A Cleared()
    {
        return new CombatWeaponTargetAssignmentsChangedEvent2A(
            string.Empty,
            Array.Empty<CombatWeaponTargetAssignment2A>());
    }
}