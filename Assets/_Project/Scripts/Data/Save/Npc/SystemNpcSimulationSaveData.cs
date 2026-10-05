using System;
using System.Collections.Generic;

[Serializable]
public sealed class SystemNpcSimulationSaveData
{
    public List<SystemNpcPopulationSnapshotEntrySaveData> PopulationEntries = new();

    // Legacy: оставляем только чтобы старые save-файлы можно было прочитать.
    // Новые сохранения это поле не заполняют.
    public List<SystemNpcSaveData> Npcs = new();

    public List<SystemPopulationRuleTimerState> PopulationTimers = new();
}

[Serializable]
public sealed class SystemNpcPopulationSnapshotEntrySaveData
{
    public string SystemId;
    public SystemNpcType NpcType;
    public string ConfigId;
    public string SpawnRuleId;
    public AllyRole2A AllyRole;
    public int Count;
    public int GroupCount;
}