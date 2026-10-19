public static class DeveloperModeGate
{
    public static bool IsEnabled
    {
        get
        {
            return IsEnabledForSettings(GetDebugSettingsFromServices());
        }
    }

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