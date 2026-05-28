using UnityEngine;

public sealed class NewGameService2A : CustomService, INewGameService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IContinueGameService _continueGameService;
    private readonly ISaveService _saveService;
    private readonly IGameStateMachine _gameStateMachine;
    private readonly NewGameFactory2A _newGameFactory;

    public NewGameService2A()
    {
        _debugStop = true;

        _gameStateMachine = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _gameSessionService = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameSessionService>();
        _continueGameService = Bootstrapper2A.Instance.ServiceRegistry.Get<IContinueGameService>();
        _saveService = Bootstrapper2A.Instance.ServiceRegistry.Get<ISaveService>();

        _newGameFactory = new NewGameFactory2A();
    }

    public void StartNewGame()
    {
        var continued = false;

        if (_continueGameService != null && _continueGameService.CanContinue())
        {
            LogCustom("NewGameService2A: save found, trying continue.");
            continued = _continueGameService.ContinueGame();
        }

        if (continued)
        {
            return;
        }

        LogCustom("NewGameService2A: creating new game.");

        var save = _newGameFactory.CreateNewGame();

        LogCustom($"[NewGame2A] Active ship: {save.PlayerProfile.PlayerShipState.ActiveShipId}");
        LogCustom($"[NewGame2A] Owned ships count: {save.PlayerProfile.PlayerShipState.OwnedShips.Count}");

        _gameSessionService.StartNewSession(save);

        _saveService.Save(save);

        LogCustom("NewGameService2A: new game session started and saved.");

        if (_gameStateMachine != null)
        {
            _gameStateMachine.Enter(new MetaState2A());
        }
    }
}