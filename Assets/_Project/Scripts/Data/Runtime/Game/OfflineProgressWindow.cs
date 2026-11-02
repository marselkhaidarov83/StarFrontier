using System;

/// <summary>The shared wall-clock budget for all consumers of offline progress.</summary>
public static class OfflineProgressWindow
{
    public const double MaxHours = 12d;
    public static double ElapsedSeconds(long fromUtcTicks, long toUtcTicks)
    {
        if (fromUtcTicks <= 0 || toUtcTicks <= fromUtcTicks || toUtcTicks > DateTime.MaxValue.Ticks)
            return 0d;
        return Math.Min((toUtcTicks - fromUtcTicks) / (double)TimeSpan.TicksPerSecond, MaxHours * 3600d);
    }
}
