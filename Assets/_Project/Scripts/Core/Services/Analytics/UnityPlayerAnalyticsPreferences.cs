using System;
using UnityEngine;

public sealed class UnityPlayerAnalyticsPreferences : IPlayerAnalyticsPreferences
{
    private const string InstallationKey = "sf.analytics.installation.v1";
    private const string EnabledKey = "sf.analytics.enabled.v1";
    private const string FirstSessionKey = "sf.analytics.first_session_pending.v1";
    private readonly string _installationId;
    public string InstallationId => _installationId;

    public UnityPlayerAnalyticsPreferences()
    {
        _installationId = PlayerPrefs.GetString(InstallationKey, "");
        if (!Guid.TryParse(_installationId, out _))
        {
            _installationId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(InstallationKey, _installationId);
            PlayerPrefs.Save();
        }
    }

    public bool CollectionEnabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
        set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public bool FirstSessionPending
    {
        get => PlayerPrefs.GetInt(FirstSessionKey, 1) != 0;
        set { PlayerPrefs.SetInt(FirstSessionKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/STAR FRONTIER/Analytics/Reset First Session")]
    private static void ResetFirstSessionForEditor()
    {
        PlayerPrefs.SetInt(FirstSessionKey, 1);
        PlayerPrefs.Save();
        UnityEditor.EditorUtility.DisplayDialog("STAR FRONTIER",
            "Признак первой сессии сброшен. Запустите Play Mode снова.", "OK");
    }
#endif
}
