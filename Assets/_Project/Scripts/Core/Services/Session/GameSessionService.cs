using UnityEngine;

public class GameSessionService : IGameSessionService
{
    public GameState State { get; private set; }

    public bool HasActiveSession => State != null;

    private ISystemTravelService _systemTravelService;

    public GameSessionService()
    {
    }

    public void StartNewSession(GameState state)
    {
        State = state;
        InitializeSystemTravelService();
    }

    public void LoadSession(GameState state)
    {
        State = state;
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
}