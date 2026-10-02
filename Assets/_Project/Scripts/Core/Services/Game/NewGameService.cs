using System.Collections;
using UnityEngine;

public class NewGameService : CustomService, INewGameService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IContinueGameService _continueGameService;
    private readonly NewGameFactory _newGameFactory;
    public readonly IGameStateMachine _gameStateMachine;

    private bool _startInProgress;

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
        if (_startInProgress)
            return;

        if (Bootstrapper.Instance == null)
        {
            AppLog.Error(
                "NewGameService cannot start loading: Bootstrapper.Instance is null.");

            return;
        }

        Bootstrapper.Instance.StartCoroutine(
            StartNewGameRoutine());
    }

    private IEnumerator StartNewGameRoutine()
    {
        _startInProgress = true;

        System.Diagnostics.Stopwatch total =
            System.Diagnostics.Stopwatch.StartNew();

        System.Diagnostics.Stopwatch stage =
            System.Diagnostics.Stopwatch.StartNew();

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.StartProgress);

        LoadingSceneContext.Log(
            "NewGameService.StartNewGame.Start");

        yield return null;

        bool canContinue =
            _continueGameService.CanContinue();

        LogLoadingStep(
            "CanContinue | Result=" + canContinue,
            total,
            stage);

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.CanContinueProgress);

        yield return null;

        if (canContinue)
        {
            bool continued = false;

            yield return _continueGameService.ContinueGameRoutine(
                result => continued = result);

            LogLoadingStep(
                "ContinueGame | Result=" + continued,
                total,
                stage);

            if (continued)
            {
                _startInProgress = false;
                yield break;
            }

            LoadingSceneContext.SetProgress(
                string.Empty,
                LoadingSceneContext.SaveLoadedProgress);

            yield return null;
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.SessionLoadedProgress);

        LogLoadingStep(
            "CreateNewGame.Before",
            total,
            stage);

        yield return null;

        GameRuntimeState save =
            _newGameFactory.CreateNewGame();

        LogLoadingStep(
            "CreateNewGame.After",
            total,
            stage);

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.OfflineRelocationFromProgress);

        yield return null;

        LogCustom(
            "[NewGame] Active ship: " +
            save.Player.PlayerShipState.ActiveShipId);

        LogCustom(
            "[NewGame] Owned ships count: " +
            save.Player.PlayerShipState.OwnedShips.Count);

        _gameSessionService.StartNewSession(save);

        LogLoadingStep(
            "StartNewSession.After",
            total,
            stage);

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.PopulationProgress);

        yield return null;

        if (_gameStateMachine == null)
        {
            LogCustom("GameStateMachine is null");

            LogLoadingStep(
                "GameStateMachine.Null",
                total,
                stage);
        }
        else
        {
            LoadingSceneContext.SetProgress(
                string.Empty,
                LoadingSceneContext.EnterSystemProgress);

            yield return null;

            LogLoadingStep(
                "EnterSystemState.Before",
                total,
                stage);

            _gameStateMachine.Enter(new SystemState());

            LogLoadingStep(
                "EnterSystemState.After",
                total,
                stage);
        }

        LogLoadingStep(
            "StartNewGame.Complete",
            total,
            stage);

        _startInProgress = false;
    }

    private static void LogLoadingStep(
    string phase,
    System.Diagnostics.Stopwatch total,
    System.Diagnostics.Stopwatch stage)
    {
        if (!IsLoadingSceneDiagnosticsLogEnabled())
        {
            stage.Restart();
            return;
        }

        Debug.Log(
            "[LOADING_DIAG][NewGameService] " +
            phase +
            " | Frame=" + Time.frameCount +
            " | Time=" + Time.unscaledTime.ToString("F3") +
            " | StageMs=" + stage.Elapsed.TotalMilliseconds.ToString("F2") +
            " | TotalMs=" + total.Elapsed.TotalMilliseconds.ToString("F2"));

        stage.Restart();
    }

    private static bool IsLoadingSceneDiagnosticsLogEnabled()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.DebugLogConfig == null)
        {
            return false;
        }

        return Bootstrapper.Instance
            .DebugLogConfig
            .LoadingSceneDiagnosticsLogs;
    }
}