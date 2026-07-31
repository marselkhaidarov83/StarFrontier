using System;

[Serializable]
public class StarSystemRuntimeState
{
    public string SystemId;

    public bool IsDiscovered;
    public bool IsVisited;

    public int DevelopmentLevel;
    public int DangerLevel;
    public int Stability;
}