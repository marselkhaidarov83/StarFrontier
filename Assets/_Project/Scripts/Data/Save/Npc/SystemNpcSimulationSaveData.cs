using System;
using System.Collections.Generic;

[Serializable]
public sealed class SystemNpcSimulationSaveData
{
    public List<SystemNpcSaveData> Npcs = new();
    public List<SystemPopulationRuleTimerState> PopulationTimers = new();
}