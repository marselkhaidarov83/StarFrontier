using System;
using System.Collections.Generic;

[Serializable]
public sealed class EnemyGroupState
{
    public string RuntimeGroupId;
    public string FactionId;
    public string SystemId;
    public string SpawnRuleId;
    public string InvasionId;

    public int Level = 1;

    public EnemyGroupLifecycleState LifecycleState =
        EnemyGroupLifecycleState.None;

    public List<string> MemberRuntimeNpcIds = new();
    public List<string> MemberConfigIds = new();

    public int CreatedAtTick;
    public int LastUpdatedTick;
    public int NextSpawnTick;
    public int CleanupAfterTick;

    public string OwnerSystemId;
    public string TargetSystemId;

    public bool IsActive()
    {
        return LifecycleState == EnemyGroupLifecycleState.Active ||
               LifecycleState == EnemyGroupLifecycleState.Spawning;
    }
}