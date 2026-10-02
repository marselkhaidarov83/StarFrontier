using UnityEngine;

public interface ISystemNpcMovementRouteService
{
    Vector3 GetNextTargetPosition(SystemNpcRuntimeState npc);

    Vector3 GetNextTargetPosition(
        SystemNpcRuntimeState npc,
        SystemNpcMovementTargetResolveStats stats);
}