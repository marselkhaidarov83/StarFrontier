using UnityEngine;

public interface IPlayerCombatTargetService
{
    bool IsPlayerAvailableInSystem(string systemId);
    Vector3 GetPlayerPosition();
    CombatDamageResult2A ApplyDamage(int damage);
}