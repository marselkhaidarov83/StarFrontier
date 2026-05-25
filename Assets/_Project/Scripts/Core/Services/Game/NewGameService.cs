using UnityEngine;

public class NewGameService : CustomService, INewGameService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IContinueGameService _continueGameService;
    private readonly NewGameFactory _newGameFactory;
    public readonly IGameStateMachine _gameStateMachine;

    public NewGameService()
    {
        _debugStop = true;
        _gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _continueGameService = Bootstrapper.Instance.ServiceRegistry.Get<IContinueGameService>();
        _newGameFactory = new NewGameFactory();
    }

    public void StartNewGame()
    {
        var _continue = false;
        // if (_continueGameService.CanContinue())
        // {
        //     LogCustom("CanContinue = true");
        //     _continue = _continueGameService.ContinueGame();
        // }
        
        if (!_continue)
        {
            LogCustom("CreateNewGame");

            var save = _newGameFactory.CreateNewGame();
            LogCustom("New game created");

            LogCustom($"[NewGame] Active ship: {save.PlayerProfile.PlayerShipState.ActiveShipId}");
            LogCustom($"[NewGame] Owned ships count: {save.PlayerProfile.PlayerShipState.OwnedShips.Count}");

            _gameSessionService.StartNewSession(save);
            LogCustom("New session started");

            if (_gameStateMachine == null)
            {
                LogCustom("GameStateMachine is null");
            }
            else 
                _gameStateMachine.Enter(new MetaState());
        }
    }
}