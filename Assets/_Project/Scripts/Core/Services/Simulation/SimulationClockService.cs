// using UnityEngine;

//     public sealed class SimulationClockService : ISimulationClockService
//     {
//         private float _accumulator;

//         public SimulationState State { get; private set; } = SimulationState.Play;
//         public int CurrentTick { get; private set; }
//         public float TickDurationSeconds { get; private set; } = 0.25f;

//         public bool IsPlaying => State == SimulationState.Play;

//         public SimulationClockService()
//         {
//             float tickDurationSeconds = 0.25f;
//             TickDurationSeconds = Mathf.Max(0.01f, tickDurationSeconds);
//         }

//         public void SetState(SimulationState state)
//         {
//             State = state;
//         }

//         public void Play()
//         {
//             State = SimulationState.Play;
//         }

//         public void Pause()
//         {
//             State = SimulationState.Paused;
//         }

//         public bool TryAdvance(float deltaTime, out int advancedTicks)
//         {
//             advancedTicks = 0;

//             if (!IsPlaying)
//                 return false;

//             if (deltaTime <= 0f)
//                 return false;

//             _accumulator += deltaTime;

//             while (_accumulator >= TickDurationSeconds)
//             {
//                 _accumulator -= TickDurationSeconds;
//                 CurrentTick++;
//                 advancedTicks++;
//             }

//             return advancedTicks > 0;
//         }
//     }