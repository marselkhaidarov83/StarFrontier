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
        var planet = configService.GetPlanetConfigById(gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId);

        if (planet == null)
            return false;

        return planet.IsInhabited;
    }
}