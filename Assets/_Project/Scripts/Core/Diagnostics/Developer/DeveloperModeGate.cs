public static class DeveloperModeGate
{
    public static bool IsEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }
}
