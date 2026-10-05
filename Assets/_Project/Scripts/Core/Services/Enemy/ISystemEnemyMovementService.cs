public interface ISystemEnemyMovementService
{
    void Tick(float deltaTime, int currentTick);

    bool TryBuildRoutePreview2A(
        string runtimeEnemyId,
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick);
}