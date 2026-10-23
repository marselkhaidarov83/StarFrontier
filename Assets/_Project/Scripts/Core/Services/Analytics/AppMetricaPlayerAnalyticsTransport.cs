using System;
using UnityEngine;
using Io.AppMetrica;

public sealed class AppMetricaPlayerAnalyticsTransport : IPlayerAnalyticsTransport
{
    private bool _active;
    public bool Start(PlayerAnalyticsOptions options, string apiKey)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (AppMetrica.IsActivated())
            throw new InvalidOperationException("AppMetrica already activated outside the player analytics service.");
        AppMetrica.Activate(CreateConfig(options, apiKey));
#endif
        _active = true;
        return true;
    }

    public static AppMetricaConfig CreateConfig(PlayerAnalyticsOptions options, string apiKey)
    {
        return new AppMetricaConfig(apiKey)
        {
            AppVersion = options.AppVersion,
            DataSendingEnabled = true,
            LocationTracking = false, AdvIdentifiersTracking = false,
            CrashReporting = false, NativeCrashReporting = false,
            AppOpenTrackingEnabled = false, RevenueAutoTrackingEnabled = false,
            SessionsAutoTrackingEnabled = false,
            SessionTimeout = Math.Max(10, options.SessionTimeoutSeconds),
            MaxReportsInDatabaseCount = Math.Max(100, Math.Min(10000, options.MaxQueuedReports)),
            MaxReportsCount = 50, DispatchPeriodSeconds = Math.Max(10, options.DispatchPeriodSeconds),
            Logs = false
        };
    }

    public void Report(PlayerAnalyticsRecord record)
    {
        if (!_active) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        // Report exactly once to the native SDK. The SDK alone persists and retries delivery.
        AppMetrica.ReportEvent(record.action, JsonUtility.ToJson(record));
#endif
        // Editor/other platforms are a local preview, never a claim of successful delivery.
    }

    public void SetEnabled(bool enabled)
    {
        if (!_active) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        AppMetrica.SetDataSendingEnabled(enabled);
#endif
    }

    public void Pause(bool paused)
    {
        if (!_active) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (paused) AppMetrica.PauseSession(); else AppMetrica.ResumeSession();
#endif
    }

    public void Flush()
    {
        if (!_active) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        AppMetrica.SendEventsBuffer();
#endif
    }
}
