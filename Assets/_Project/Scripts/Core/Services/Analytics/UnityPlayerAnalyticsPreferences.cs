using System;
using UnityEngine;

public sealed class UnityPlayerAnalyticsPreferences : IPlayerAnalyticsPreferences
{
    private const string InstallationKey = "sf.analytics.installation.v1";
    private const string EnabledKey = "sf.analytics.enabled.v1";
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
}
