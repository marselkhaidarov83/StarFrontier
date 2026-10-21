public static class PlanetLandingRules2A
{
    public const string BlockedMessage = "Посадка запрещена: система принадлежит врагам";

    public static bool IsBlocked(GameRuntimeState state, DebugSettings debugSettings)
    {
        if (debugSettings != null && debugSettings.mobileTestMode &&
            !debugSettings.mobileTestBlockEnemyPlanetLanding)
        {
            return false;
        }

        if (state?.Player == null || state.Galaxy?.Systems == null ||
            string.IsNullOrWhiteSpace(state.Player.CurrentSystemId))
        {
            return false;
        }

        foreach (StarSystemRuntimeState system in state.Galaxy.Systems)
        {
            if (system != null && system.SystemId == state.Player.CurrentSystemId)
            {
                // Invasion is still our system. Only completed enemy capture blocks landing.
                return system.SystemStatus == StarSystemStatus.Captured;
            }
        }

        return false;
    }
}
