using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int SaveVersion = 1;
    public string CreatedUtc = DateTime.UtcNow.ToString();
    public string LastSavedUtc;

    public PlayerProfileData PlayerProfile = new();
    public List<MarketRuntimeData> Markets = new();
    public RuntimeMissionSaveBlock MissionBlock = new();
    public GameSettingsData Settings = new();
    public SystemEncounterSaveData SystemEncounter = new();
    public SystemNpcSimulationSaveData SystemNpcSimulation = new();

    public GameState GameState;
}