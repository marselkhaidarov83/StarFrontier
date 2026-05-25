using System;
using System.Collections.Generic;

[Serializable]
public sealed class SystemEncounterSaveData
{
    public bool HasEncounter;

    public string EncounterId;
    public string SystemId;

    public SystemEncounterState State;
    public SystemEncounterDefeatReason DefeatReason;

    public int EnemiesAlive;
    public int AlliesAlive;

    public int PlayerKills;
    public bool PlayerDestroyed;

    public bool PlayerIsOnPlanet;
    public int DaysPlayerStayedOnPlanetDuringEncounter;

    public List<SystemEnemySaveData> Enemies = new();
    public List<SystemAllySaveData> Allies = new();
}