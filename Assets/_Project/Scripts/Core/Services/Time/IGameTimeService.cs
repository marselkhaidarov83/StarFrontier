public interface IGameTimeService
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
}