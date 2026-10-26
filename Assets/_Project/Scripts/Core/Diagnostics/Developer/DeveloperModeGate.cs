public static class DeveloperModeGate
{
    public static bool IsEnabled
    {
        get
        {
            return IsEnabledForSettings(GetDebugSettingsFromServices());
        }
    }

    // Explicit Editor context-menu commands do not depend on whether the debug HUD is shown.
    // Runtime UI keeps using IsEnabled; outside the Editor the developer gate still applies.
    public static bool CanRunExplicitCommand(bool isPlaying, bool isEditor, bool developerModeEnabled) =>
        isPlaying && (isEditor || developerModeEnabled);

    public static bool IsEnabledForSettings(DebugSettings debugSettings)
    {
        if (debugSettings == null || !debugSettings.mobileTestMode)
            return false;

#if UNITY_EDITOR
        return debugSettings.showDebugOverlayInEditor;
#elif UNITY_ANDROID || UNITY_IOS
        return debugSettings.showDebugOverlayInMobileBuild;
#else
        return false;
#endif
    }

    private static DebugSettings GetDebugSettingsFromServices()
    {
        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null ||
            bootstrapper.ServiceRegistry == null)
        {
            return null;
        }

        if (!bootstrapper.ServiceRegistry.TryGet<IConfigService>(
                out IConfigService configService))
        {
            return null;
        }

        return configService.DebugSettings;
    }
}