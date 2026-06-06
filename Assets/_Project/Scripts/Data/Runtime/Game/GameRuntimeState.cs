using System;
using System.Collections.Generic;

[Serializable]
public class GameRuntimeState
{
    public GameRuntimeMetaState Meta = new ();
    public PlayerState Player = new();
    public GalaxyRuntimeState Galaxy = new ();

    public GameSettingsState Settings = new();

    public List<MarketRuntimeData> Markets = new();
    public RuntimeMissionSaveBlock MissionBlock = new();
    public SystemEncounterSaveData SystemEncounter = new();
    public SystemNpcSimulationSaveData SystemNpcSimulation = new();
}