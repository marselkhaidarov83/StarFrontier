internal static class SystemExitTargetColor2A
{
    public static string GetColor(bool hasEnoughFuel, bool isEnemySystem)
    {
        if (!hasEnoughFuel)
            return "#FF5555";
        return isEnemySystem ? "#F2D16B" : "#FFFFFF";
    }

    public static bool IsEnemySystem(GalaxyRuntimeState galaxy, string systemId)
    {
        if (galaxy?.Systems == null || string.IsNullOrWhiteSpace(systemId))
            return false;
        foreach (StarSystemRuntimeState system in galaxy.Systems)
        {
            if (system != null && system.SystemId == systemId)
                return system.SystemStatus == StarSystemStatus.Captured;
        }
        return false;
    }
}
