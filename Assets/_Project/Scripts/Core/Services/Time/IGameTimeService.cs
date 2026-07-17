public interface IGameTimeService : ITickable
{
    GameTimeState State { get; }

    int CurrentQuantTick { get; }

    bool IsPaused { get; }

    float SimulationTimeSeconds { get; }

    float DelayTime { get; }

    void SetPaused(bool paused);

    void TogglePause();

    void StepOneDay();

    void Tick(float deltaTime);

    void WriteTimeToSave(GameRuntimeState state);

    void RestoreTimeFromSave(GameRuntimeState state);
}