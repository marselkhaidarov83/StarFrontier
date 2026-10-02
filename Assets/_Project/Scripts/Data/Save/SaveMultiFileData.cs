using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveMultiFileManifest
{
    public int FormatVersion = 1;
    public string SaveId;
    public long CreatedUtcTicks;
    public int SaveDataVersion;
    public int SaveVersion;
    public int PartCount;
    public int TotalJsonChars;
    public List<SaveMultiFilePartInfo> Parts = new();
}

[Serializable]
public sealed class SaveMultiFilePartInfo
{
    public string Name;
    public string FileName;
    public string Kind;
    public int Index;
    public int JsonChars;
    public string Sha256;
}

[Serializable]
public sealed class SaveCorePartData
{
    public GameRuntimeMetaState Meta = new();
    public PlayerState Player = new();
    public GameSettingsState Settings = new();
    public List<MarketRuntimeData> Markets = new();
}

[Serializable]
public sealed class SaveGalaxyPartData
{
    public GalaxyRuntimeState Galaxy = new();
}

[Serializable]
public sealed class SaveMissionPartData
{
    public RuntimeMissionSaveBlock MissionBlock = new();
}

[Serializable]
public sealed class SaveEncounterPartData
{
    public SystemEncounterSaveData SystemEncounter = new();
}

[Serializable]
public sealed class SaveNpcPartData
{
    public List<SystemNpcPopulationSnapshotEntrySaveData> PopulationEntries = new();
}

[Serializable]
public sealed class SaveNpcTimersPartData
{
    public List<SystemPopulationRuleTimerState> PopulationTimers = new();
}