public interface ISystemNpcMovementService
{
    void Tick(StarSystemConfig starSystem, float deltaTime, int currentTick);

    bool TryBuildRoutePreview2A(
        string runtimeNpcId,
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick);
}