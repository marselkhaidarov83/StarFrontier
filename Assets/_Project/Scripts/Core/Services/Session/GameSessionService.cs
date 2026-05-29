using UnityEngine;

public class GameSessionService : IGameSessionService
{
    public SaveData CurrentSave { get; private set; }

    public bool HasActiveSession => CurrentSave != null;

    private ISystemTravelService _systemTravelService;

    // public GameSessionService(SaveRoot saveRoot)
    // {
    //     CurrentSave = saveRoot;
    // }

    public GameSessionService()
    {
    }

    public void StartNewSession(SaveData saveRoot)
    {
        CurrentSave = saveRoot;
        InitializeSystemTravelService();
    }

    public void LoadSession(SaveData saveRoot)
    {
        CurrentSave = saveRoot;
        InitializeSystemTravelService();
    }

    private void InitializeSystemTravelService()
    {
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _systemTravelService.State.SetCurrentPosition(CurrentSave.PlayerProfile.SystemMapShipPosition);
    }

    public void ClearSession()
    {
        CurrentSave = null;
    }
}