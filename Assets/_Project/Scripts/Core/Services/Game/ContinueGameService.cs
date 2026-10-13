using System;
using System.Collections;
using UnityEngine;

public class ContinueGameService : CustomService, IContinueGameService
{
    private readonly ISaveService _saveService;
    private readonly IGameSessionService _gameSessionService;
    public readonly IGameStateMachine _gameStateMachine;
    private readonly IConfigService _configService;
    private readonly ISystemNpcOfflineRelocationService _npcOfflineRelocationService;
    private readonly ISystemNpcPopulationService _npcPopulationService;
    private readonly ISystemNpcSimulationSaveService _npcSimulationSaveService;
    private readonly IInvasionService _invasionService;

    public ContinueGameService()
    {
        _gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _invasionService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _npcOfflineRelocationService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _npcPopulationService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _npcSimulationSaveService);
    }

    public bool CanContinue()
    {
        return _saveService.HasSave();
    }

    public bool ContinueGame()
    {
        System.Diagnostics.Stopwatch total =
            System.Diagnostics.Stopwatch.StartNew();

        System.Diagnostics.Stopwatch stage =
            System.Diagnostics.Stopwatch.StartNew();

        LoadingSceneContext.SetProgress(
            "Проверяем сохранение",
            0.10f);

        LoadingSceneContext.Log(
            "ContinueGameService.ContinueGame.Start");

        bool hasSave =
            _saveService.HasSave();

        LogLoadingStep(
            "HasSave | Result=" + hasSave,
            total,
            stage);

        if (!hasSave)
        {
            if (_debugEnabled)
                Debug.LogWarning(
                    "ContinueGame failed: save file does not exist.");

            LogLoadingStep(
                "ContinueGame.NoSave",
                total,
                stage);

            return false;
        }

        LoadingSceneContext.SetProgress(
            "Читаем сохранение",
            0.20f);

        GameRuntimeState save =
            _saveService.Load();

        LogLoadingStep(
            "SaveService.Load.After | SaveNull=" + (save == null),
            total,
            stage);

        if (save == null)
        {
            if (_debugEnabled)
                Debug.LogError(
                    "ContinueGame failed: save could not be loaded.");

            LogLoadingStep(
                "ContinueGame.SaveNull",
                total,
                stage);

            return false;
        }

        LoadingSceneContext.SetProgress(
            "Восстанавливаем сессию",
            0.35f);

        _gameSessionService.LoadSession(save);
        ProcessOfflineWarCatchUp(save);

        LogLoadingStep(
            "LoadSession.After",
            total,
            stage);

        if (_npcOfflineRelocationService != null)
        {
            LoadingSceneContext.SetProgress(
                "Обновляем мир после отсутствия",
                0.50f);

            _npcOfflineRelocationService.TryProcessOffline(save);

            LogLoadingStep(
                "OfflineRelocation.After",
                total,
                stage);
        }
        else
        {
            LogLoadingStep(
                "OfflineRelocation.Skipped",
                total,
                stage);
        }

        LoadingSceneContext.SetProgress(
            "Проверяем население системы",
            0.65f);

        EnsureMinimumPopulationForLoadedCurrentSystem(save);

        LogLoadingStep(
            "EnsureMinimumPopulation.After",
            total,
            stage);

        LoadingSceneContext.SetProgress(
            "Запускаем систему",
            0.80f);

        if (_gameStateMachine == null)
        {
            if (_debugEnabled)
                Debug.Log("GameStateMachine is null");

            LogLoadingStep(
                "GameStateMachine.Null",
                total,
                stage);
        }
        else
        {
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

        LoadingSceneContext.SetProgress(
            "Готовим сцену",
            0.90f);

        LogLoadingStep(
            "ContinueGame.Complete",
            total,
            stage);

        return true;
    }

    public IEnumerator ContinueGameRoutine(Action<bool> completed)
    {
        System.Diagnostics.Stopwatch total =
            System.Diagnostics.Stopwatch.StartNew();

        System.Diagnostics.Stopwatch stage =
            System.Diagnostics.Stopwatch.StartNew();

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.CanContinueProgress);

        LoadingSceneContext.Log(
            "ContinueGameService.ContinueGameRoutine.Start");

        yield return null;

        bool hasSave =
            _saveService.HasSave();

        LogLoadingStep(
            "HasSave | Result=" + hasSave,
            total,
            stage);

        if (!hasSave)
        {
            if (_debugEnabled)
                Debug.LogWarning(
                    "ContinueGame failed: save file does not exist.");

            LogLoadingStep(
                "ContinueGame.NoSave",
                total,
                stage);

            completed?.Invoke(false);
            yield break;
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.SaveLoadedProgress);

        yield return null;

        GameRuntimeState save =
            _saveService.Load();

        double saveLoadMs =
            stage.Elapsed.TotalMilliseconds;

        LoadingSceneContext.RecordSaveLoadMs(
            saveLoadMs);

        LogLoadingStep(
            "SaveService.Load.After | SaveNull=" + (save == null),
            total,
            stage);

        if (save == null)
        {
            if (_debugEnabled)
                Debug.LogError(
                    "ContinueGame failed: save could not be loaded.");

            LogLoadingStep(
                "ContinueGame.SaveNull",
                total,
                stage);

            completed?.Invoke(false);
            yield break;
        }

        LoadingSceneContext.ApplyRuntimeProgressProfile(
            save.Meta != null
                ? save.Meta.LoadingProgressProfile
                : null);

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.SessionLoadedProgress);

        yield return null;

        System.Diagnostics.Stopwatch sessionStage =
            System.Diagnostics.Stopwatch.StartNew();

        _gameSessionService.LoadSession(save);
        ProcessOfflineWarCatchUp(save);

        if (_npcSimulationSaveService != null)
        {
            LogLoadingStep(
                "NpcSimulationRestore.Before | SaveNpcCount=" +
                (save.SystemNpcSimulation != null &&
                 save.SystemNpcSimulation.Npcs != null
                    ? save.SystemNpcSimulation.Npcs.Count
                    : -1),
                total,
                stage);

            _npcSimulationSaveService.Restore(
                save.SystemNpcSimulation);

            LogLoadingStep(
                "NpcSimulationRestore.After",
                total,
                stage);
        }
        else
        {
            LogLoadingStep(
                "NpcSimulationRestore.Skipped",
                total,
                stage);
        }

        LoadingSceneContext.RecordSessionLoadMs(
            sessionStage.Elapsed.TotalMilliseconds);

        LogLoadingStep(
            "LoadSession.After | SessionLoadMs=" +
            sessionStage.Elapsed.TotalMilliseconds.ToString("F2"),
            total,
            stage);

        if (_npcOfflineRelocationService != null)
        {
            LoadingSceneContext.SetProgress(
                string.Empty,
                LoadingSceneContext.OfflineRelocationFromProgress);

            yield return null;

            bool offlineProcessed = false;

            yield return _npcOfflineRelocationService.TryProcessOfflineRoutine(
                save,
                LoadingSceneContext.OfflineRelocationFromProgress,
                LoadingSceneContext.OfflineRelocationToProgress,
                result => offlineProcessed = result);

            LoadingSceneContext.RecordOfflineRelocationMs(
                stage.Elapsed.TotalMilliseconds);

            LogLoadingStep(
                "OfflineRelocation.After | Result=" + offlineProcessed,
                total,
                stage);
        }
        else
        {
            LoadingSceneContext.RecordOfflineRelocationMs(0d);

            LogLoadingStep(
                "OfflineRelocation.Skipped",
                total,
                stage);
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.PopulationProgress);

        yield return null;

        EnsureMinimumPopulationForLoadedCurrentSystem(save);

        LoadingSceneContext.RecordPopulationMs(
            stage.Elapsed.TotalMilliseconds);

        LogLoadingStep(
            "EnsureMinimumPopulation.After",
            total,
            stage);

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.EnterSystemProgress);

        yield return null;

        if (_gameStateMachine == null)
        {
            if (_debugEnabled)
                Debug.Log("GameStateMachine is null");

            LogLoadingStep(
                "GameStateMachine.Null",
                total,
                stage);
        }
        else
        {
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
            "ContinueGame.Complete",
            total,
            stage);

        completed?.Invoke(true);
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
            "[LOADING_DIAG][ContinueGameService] " +
            phase +
            " | Frame=" + Time.frameCount +
            " | Time=" + Time.unscaledTime.ToString("F3") +
            " | StageMs=" + stage.Elapsed.TotalMilliseconds.ToString("F2") +
            " | TotalMs=" + total.Elapsed.TotalMilliseconds.ToString("F2"));

        stage.Restart();
    }

    private void EnsureMinimumPopulationForLoadedCurrentSystem(
        GameRuntimeState save)
    {
        if (_npcPopulationService == null)
            return;

        if (save == null ||
            save.Galaxy == null ||
            string.IsNullOrWhiteSpace(save.Galaxy.CurrentSystemId))
            return;

        StarSystemConfig currentSystem =
            _configService.GetStarSystemConfigById(
                save.Galaxy.CurrentSystemId);

        if (currentSystem == null)
            return;

        _npcPopulationService.EnsureMinimumAlliesInSystem(
            currentSystem);
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

    private void ProcessOfflineWarCatchUp(
    GameRuntimeState save)
    {
        if (_invasionService == null ||
            save == null ||
            save.Meta == null)
        {
            return;
        }

        int targetQuantTick =
            0;

        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(
                out IGameTimeService gameTimeService))
        {
            targetQuantTick =
                gameTimeService.CurrentQuantTick;
        }

        if (targetQuantTick <= 0 &&
            save.Galaxy != null)
        {
            targetQuantTick =
                Math.Max(
                    1,
                    save.Galaxy.GalaxyDay);
        }

        int changedCount =
            _invasionService.ProcessOfflineWarCatchUp(
                save,
                targetQuantTick);

        LogCustom(
            "[ContinueGameService] Offline war catch-up complete. " +
            "ChangedCount: " + changedCount +
            " | TargetQuantTick: " + targetQuantTick);
    }
}