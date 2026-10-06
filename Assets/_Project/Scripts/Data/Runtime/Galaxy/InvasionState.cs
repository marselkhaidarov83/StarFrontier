using System;
using System.Collections.Generic;

[Serializable]
public sealed class InvasionState
{
    public string InvasionId;
    public string FactionId;

    public string SourceSystemId;
    public string TargetSystemId;

    public int Level = 1;

    public InvasionLifecycleState LifecycleState =
        InvasionLifecycleState.None;

    public List<string> EnemyGroupRuntimeIds = new();

    public int StartedAtTick;
    public int LastUpdatedTick;
    public int NextUpdateTick;
    public int ResolveAtTick;
    public int CleanupAfterTick;

    public bool IsActive()
    {
        return LifecycleState == InvasionLifecycleState.Preparing ||
               LifecycleState == InvasionLifecycleState.Active;
    }
}