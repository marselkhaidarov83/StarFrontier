public interface ISystemNpcOfflineRelocationService
{
    bool TryProcessOffline(GameRuntimeState state);

    bool DebugForceTargetNpcRoute(
        string runtimeNpcId,
        GameRuntimeState state);

    bool DebugProcessOfflineStep(
        GameRuntimeState state,
        float offlineHours);
}