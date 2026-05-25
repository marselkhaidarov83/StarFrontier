using System;
using UnityEngine;

public sealed class GovernmentRewardService : CustomService, IGovernmentRewardService
{
    private readonly ISystemEncounterService _encounterService;
    private readonly ISystemEnemyService _enemyService;
    private readonly IGovernmentRewardPayoutService _payoutService;
    private readonly SimpleEventBus _eventBus;

    public GovernmentRewardService()
    {
        _debugStop = true;
        _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
        _enemyService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEnemyService>();
        _payoutService = Bootstrapper.Instance.ServiceRegistry.Get<IGovernmentRewardPayoutService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    public bool CanClaimReward(string currentSystemId, bool isCurrentPlanetInhabited)
    {
        ActiveSystemEncounter encounter = _encounterService.Current;

        if (encounter == null)
        {
            LogCustom("encounter = " + encounter);
            return false;
        }

        if (encounter.State != SystemEncounterState.VictoryPendingReward)
        {
            LogCustom("encounter.State = " + encounter.State);
            return false;
        }

        if (!isCurrentPlanetInhabited)
        {
            LogCustom("isCurrentPlanetInhabited = " + isCurrentPlanetInhabited);
            return false;
        }

        if (encounter.SystemId != currentSystemId)
        {
            Debug.Log("[GovernmentRewardService] encounter.SystemId = " + encounter.SystemId);
            Debug.Log("[GovernmentRewardService] currentSystemId = " + currentSystemId);
            return false;
        }

        if (encounter.PlayerKills <= 0)
        {
            Debug.Log("[GovernmentRewardService] encounter.PlayerKills = " + encounter.PlayerKills);
            return false;
        }

        return true;
    }

    public GovernmentRewardResult ClaimReward(
        string currentSystemId,
        bool isCurrentPlanetInhabited)
    {
        ActiveSystemEncounter encounter = _encounterService.Current;

        if (encounter == null)
            return GovernmentRewardResult.Failed("No active encounter.");

        if (!CanClaimReward(currentSystemId, isCurrentPlanetInhabited))
            return GovernmentRewardResult.Failed("Reward is not available.");

        int credits = CalculateCredits(encounter.SystemId);
        int xp = CalculateXp(encounter.SystemId);

        _payoutService.Grant(credits, xp);

        bool resolved = _encounterService.TryResolvePendingReward();

        if (!resolved)
            return GovernmentRewardResult.Failed("Encounter could not be resolved.");

        _eventBus.Publish(new GovernmentRewardClaimedEvent(
            encounter.EncounterId,
            encounter.SystemId,
            credits,
            xp));

        Debug.Log(
            $"[GovernmentRewardService] Reward claimed. Credits: {credits}, XP: {xp}"
        );

        return GovernmentRewardResult.Granted(credits, xp);
    }

    private int CalculateCredits(string systemId)
    {
        int total = 0;

        foreach (SystemEnemyRuntimeState enemy in _enemyService.Enemies)
        {
            if (enemy.SystemId != systemId)
                continue;

            if (enemy.IsAlive)
                continue;

            if (enemy.EnemyConfig == null)
                continue;

            total += enemy.EnemyConfig.CreditReward;
        }

        return Mathf.Max(0, total);
    }

    private int CalculateXp(string systemId)
    {
        int total = 0;

        foreach (SystemEnemyRuntimeState enemy in _enemyService.Enemies)
        {
            if (enemy.SystemId != systemId)
                continue;

            if (enemy.IsAlive)
                continue;

            if (enemy.EnemyConfig == null)
                continue;

            total += enemy.EnemyConfig.XpReward;
        }

        return Mathf.Max(0, total);
    }
}