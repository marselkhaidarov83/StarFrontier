using System.Collections.Generic;

public sealed class SystemNpcSimulationCaptureSession
{
    public readonly Dictionary<string, SystemNpcPopulationCaptureAggregateData> Aggregates =
        new();

    public readonly SystemNpcSimulationSaveData SaveData =
        new();

    public int NextRuntimeNpcIndex;
    public int RuntimeNpcs;
    public int AliveNpcs;
    public int SkippedDeadNpcs;
    public int SkippedInvalidNpcs;
    public int SnapshotNpcCount;
    public bool RuntimeScanComplete;
    public bool EntriesBuildComplete;
    public List<SystemNpcPopulationCaptureAggregateData> AggregateList;
    public int NextAggregateIndex;

    public bool IsComplete =>
        RuntimeScanComplete && EntriesBuildComplete;
}

public sealed class SystemNpcPopulationCaptureAggregateData
{
    public string SystemId;
    public SystemNpcType NpcType;
    public string ConfigId;
    public string SpawnRuleId;
    public AllyRole2A AllyRole;
    public int Count;
    public HashSet<string> GroupRuntimeIds = new();

    public SystemNpcPopulationSnapshotEntrySaveData ToSaveData()
    {
        return new SystemNpcPopulationSnapshotEntrySaveData
        {
            SystemId = SystemId,
            NpcType = NpcType,
            ConfigId = ConfigId,
            SpawnRuleId = SpawnRuleId,
            AllyRole = AllyRole,
            Count = Count,
            GroupCount = GroupRuntimeIds.Count
        };
    }
}