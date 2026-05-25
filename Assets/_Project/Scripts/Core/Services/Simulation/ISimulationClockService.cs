// public interface ISimulationClockService
//     {
//         SimulationState State { get; }
//         int CurrentTick { get; }
//         float TickDurationSeconds { get; }

//         bool IsPlaying { get; }

//         void SetState(SimulationState state);
//         void Play();
//         void Pause();

//         bool TryAdvance(float deltaTime, out int advancedTicks);
//     }