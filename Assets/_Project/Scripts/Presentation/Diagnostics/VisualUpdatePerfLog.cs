using UnityEngine;

public static class VisualUpdatePerfLog
{
    private const double FallbackThresholdMs = 1.0;

    public static void LogIfSlow(
        string marker,
        double startedAt,
        string details)
    {
        double elapsedMs =
            (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

        DebugLogConfig config =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        double thresholdMs =
            config != null
                ? config.VisualUpdateSpikeThresholdMs
                : FallbackThresholdMs;

        if (elapsedMs < thresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[VISUAL_UPDATE_SPIKE]" +
            " Marker=" + marker +
            " | UnityFrame=" + Time.frameCount +
            " | Ms=" + elapsedMs.ToString("F2") +
            " | ThresholdMs=" + thresholdMs.ToString("F2") +
            " | " + details);
    }

    public static bool VerboseEnabled =>
        Bootstrapper.Instance != null &&
        Bootstrapper.Instance.DebugLogConfig != null &&
        Bootstrapper.Instance.DebugLogConfig.VisualUpdateVerboseLogs;

    public static bool ShouldLog(
double elapsedMs)
    {
        DebugLogConfig config =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        double thresholdMs =
            config != null
                ? config.VisualUpdateSpikeThresholdMs
                : FallbackThresholdMs;

        if (elapsedMs < thresholdMs)
            return false;

        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsPerformanceLogEnabled(
                   DebugLogPerformanceArea.GameTimeLoadAnalytics);
    }

    public static void LogMeasured(
        string marker,
        double elapsedMs,
        string details)
    {
        if (!ShouldLog(elapsedMs))
            return;

        DebugLogConfig config =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        double thresholdMs =
            config != null
                ? config.VisualUpdateSpikeThresholdMs
                : FallbackThresholdMs;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[VISUAL_UPDATE_SPIKE]" +
            " Marker=" + marker +
            " | UnityFrame=" + Time.frameCount +
            " | Ms=" + elapsedMs.ToString("F2") +
            " | ThresholdMs=" + thresholdMs.ToString("F2") +
            " | " + details);
    }
}