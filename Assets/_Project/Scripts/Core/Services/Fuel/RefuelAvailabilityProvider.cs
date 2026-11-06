public class PlanetRefuelAvailabilityProvider : IRefuelAvailabilityProvider
{
    private IGameSessionService gameSessionService;
    private IConfigService configService;
    
    public PlanetRefuelAvailabilityProvider()
    {
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
    }

    public bool IsRefuelAvailable()
    {
        var state = gameSessionService.State;
        if (state?.Player == null) return false;
        if (!string.IsNullOrEmpty(state.Player.CurrentStationId))
            return StationLocationRules.IsInside(state, configService.GetStarSystemConfigById(state.Player.CurrentSystemId));
        var planet = configService.GetPlanetConfigById(state.Player.CurrentPlanetId);

        if (planet == null)
            return false;

        return planet.IsInhabited;
    }
}