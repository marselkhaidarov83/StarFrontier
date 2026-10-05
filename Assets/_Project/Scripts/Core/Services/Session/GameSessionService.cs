using UnityEngine;

public class GameSessionService : IGameSessionService
{
    public GameRuntimeState State { get; private set; }

    public bool HasActiveSession => State != null;

    private ISystemTravelService _systemTravelService;

    public GameSessionService()
    {
    }

    public void StartNewSession(GameRuntimeState state)
    {
        State = state;
        ForcePauseOnSessionOpen();
        InitializeSystemTravelService();
    }

    public void LoadSession(GameRuntimeState state)
    {
        State = state;
        ForcePauseOnSessionOpen();
        InitializeSystemTravelService();
    }

    private void InitializeSystemTravelService()
    {
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _systemTravelService.State.SetCurrentPosition(State.Player.SystemMapShipPosition);
    }

    public void ClearSession()
    {
        State = null;
    }

    private void ForcePauseOnSessionOpen()
    {
        if (State?.Meta != null)
            State.Meta.IsGameTimePaused = true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService))
        {
            gameTimeService.SetPaused(true);
        }
    }
}
