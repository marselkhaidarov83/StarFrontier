using System;

[Serializable]
public class GameTimeState
{
    public int CurrentQuantTick = 0;
    public bool IsPaused = true;

    public static float SecondsPerDay = 1f;
    public float SecondsPerDayTimeout = 0.1f;
    public float Accumulator = 0f;

    public float SimulationTimeSeconds = 0f;
}