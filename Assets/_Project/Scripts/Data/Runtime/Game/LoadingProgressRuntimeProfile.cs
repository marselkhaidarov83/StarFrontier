using System;

[Serializable]
public sealed class LoadingProgressRuntimeProfile
{
    public bool HasSamples;
    public int SampleCount;

    public float SaveLoadMs;
    public float SessionLoadMs;
    public float OfflineRelocationMs;
    public float PopulationMs;
    public float WarmupMs;

    public float SaveLoadedProgress;
    public float SessionLoadedProgress;
    public float OfflineRelocationFromProgress;
    public float OfflineRelocationToProgress;
    public float PopulationProgress;
    public float EnterSystemProgress;
    public float WarmupFromProgress;
    public float WarmupToProgress;
}