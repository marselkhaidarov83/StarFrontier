using System;
using System.Collections;

public interface ISystemNpcOfflineRelocationService
{
    void InitializeMilitaryStartLocations(GameRuntimeState state);

    bool TryProcessOffline(GameRuntimeState state);

    IEnumerator TryProcessOfflineRoutine(
        GameRuntimeState state,
        float progressFrom01,
        float progressTo01,
        Action<bool> completed);

    bool DebugForceTargetNpcRoute(
        string runtimeNpcId,
        GameRuntimeState state);

    bool DebugProcessOfflineStep(
        GameRuntimeState state,
        float offlineHours);
}