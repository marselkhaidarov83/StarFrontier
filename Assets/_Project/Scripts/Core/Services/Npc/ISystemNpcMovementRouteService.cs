using UnityEngine;

public interface ISystemNpcMovementRouteService
{
    Vector3 GetNextTargetPosition(SystemNpcRuntimeState npc);
}