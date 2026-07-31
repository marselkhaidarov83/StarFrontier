using System;
using System.Collections.Generic;

[Serializable]
public class GameState
{
    public GameMetaState Meta = new ();
    public PlayerState Player = new();
    public GalaxyState Galaxy = new ();

    public GameSettingsState Settings = new();

    public List<MarketRuntimeData> Markets = new();
    public RuntimeMissionSaveBlock MissionBlock = new();
    public SystemEncounterSaveData SystemEncounter = new();
    public SystemNpcSimulationSaveData SystemNpcSimulation = new();
}