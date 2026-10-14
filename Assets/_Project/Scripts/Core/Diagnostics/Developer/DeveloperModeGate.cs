public static class DeveloperModeGate
{
    public static bool IsEnabled
    {
        get
        {
            return IsEnabledForConfig(GetDebugConfigFromServices());
        }
    }

    public static bool IsEnabledForConfig(DebugConfig debugConfig)
    {
        if (debugConfig == null)
            return false;

#if UNITY_EDITOR
        return debugConfig.showDebugOverlayInEditor;
#elif UNITY_ANDROID || UNITY_IOS
        return debugConfig.showDebugOverlayInMobileBuild;
#else
        return false;
#endif
    }

    private static DebugConfig GetDebugConfigFromServices()
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

        return configService.DebugConfig;
    }
}