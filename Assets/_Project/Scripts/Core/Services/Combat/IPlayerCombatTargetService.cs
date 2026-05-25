using UnityEngine;

public interface IPlayerCombatTargetService
{
    bool IsPlayerAvailableInSystem(string systemId);
    Vector3 GetPlayerPosition();
    void ApplyDamage(int damage);
}