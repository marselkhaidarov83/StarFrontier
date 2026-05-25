using System;
using UnityEngine;

public sealed class SystemEncounterService : CustomService, ISystemEncounterService
{
    private const int PlanetStayDefeatDays = 3;

    private readonly SimpleEventBus _eventBus;

    public ActiveSystemEncounter Current { get; private set; }

    public bool HasActiveEncounter =>
        Current != null && Current.State == SystemEncounterState.Active;

    public bool HasPendingGovernmentReward =>
        Current != null && Current.State == SystemEncounterState.VictoryPendingReward;

    public SystemEncounterService()
    {
        _debugStop = true;
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    public void StartEncounter(string encounterId, string systemId, int enemyCount, int allyCount)
    {
        if (string.IsNullOrWhiteSpace(encounterId))
            throw new ArgumentException("EncounterId is empty.", nameof(encounterId));

        if (string.IsNullOrWhiteSpace(systemId))
            throw new ArgumentException("SystemId is empty.", nameof(systemId));

        if (enemyCount <= 0)
            throw new ArgumentException("Enemy count must be greater than zero.", nameof(enemyCount));

        if (allyCount < 0)
            throw new ArgumentException("Ally count cannot be negative.", nameof(allyCount));

        Current = new ActiveSystemEncounter
        {
            EncounterId = encounterId,
            SystemId = systemId,
            State = SystemEncounterState.Active,

            EnemiesAlive = enemyCount,
            AlliesAlive = allyCount,

            PlayerKills = 0,
            PlayerDestroyed = false,

            PlayerIsOnPlanet = false,
            DaysPlayerStayedOnPlanetDuringEncounter = 0,

            DefeatReason = SystemEncounterDefeatReason.None
        };

        Debug.Log(
            $"[SystemEncounterService] Started. Encounter: {encounterId}, System: {systemId}, Enemies: {enemyCount}, Allies: {allyCount}"
        );

        _eventBus.Publish(new SystemEncounterStartedEvent(
            Current.EncounterId,
            Current.SystemId,
            Current.EnemiesAlive,
            Current.AlliesAlive));

        PublishStateChanged();
    }

    public void RegisterEnemyDestroyed(bool killedByPlayer)
    {
        if (!HasActiveEncounter)
            return;

        Current.EnemiesAlive--;

        if (Current.EnemiesAlive < 0)
            Current.EnemiesAlive = 0;

        if (killedByPlayer)
            Current.PlayerKills++;

        Debug.Log(
            $"[SystemEncounterService] Enemy destroyed. KilledByPlayer: {killedByPlayer}, EnemiesAlive: {Current.EnemiesAlive}, PlayerKills: {Current.PlayerKills}"
        );

        CheckVictory();
    }

    public void RegisterAllyDestroyed()
    {
        if (!HasActiveEncounter)
            return;

        Current.AlliesAlive--;

        if (Current.AlliesAlive < 0)
            Current.AlliesAlive = 0;

        Debug.Log(
            $"[SystemEncounterService] Ally destroyed. AlliesAlive: {Current.AlliesAlive}"
        );

        CheckAlliesDestroyedDefeat();
    }

    public void RegisterPlayerDestroyed()
    {
        if (!HasActiveEncounter)
            return;

        Current.PlayerDestroyed = true;

        Debug.Log("[SystemEncounterService] Player destroyed registered.");

        Defeat(SystemEncounterDefeatReason.PlayerDestroyed);
    }

    public void RegisterPlayerEnteredPlanet()
    {
        if (!HasActiveEncounter)
            return;

        Current.PlayerIsOnPlanet = true;
        Current.DaysPlayerStayedOnPlanetDuringEncounter = 0;

        Debug.Log("[SystemEncounterService] Player entered planet during encounter.");

        CheckAlliesDestroyedDefeat();
    }

    public void RegisterPlayerLaunchedFromPlanet()
    {
        if (!HasActiveEncounter)
            return;

        Current.PlayerIsOnPlanet = false;
        Current.DaysPlayerStayedOnPlanetDuringEncounter = 0;

        Debug.Log("[SystemEncounterService] Player launched from planet during encounter.");
    }

    public void RegisterPlanetDayPassed()
    {
        if (!HasActiveEncounter)
            return;

        if (!Current.PlayerIsOnPlanet)
            return;

        Current.DaysPlayerStayedOnPlanetDuringEncounter++;

        Debug.Log(
            $"[SystemEncounterService] Planet day passed. Days: {Current.DaysPlayerStayedOnPlanetDuringEncounter}/{PlanetStayDefeatDays}"
        );

        CheckAlliesDestroyedDefeat();
    }

    public void RegisterPlayerLeftSystem(string fromSystemId)
    {
        if (!HasActiveEncounter)
            return;

        if (Current.SystemId != fromSystemId)
            return;

        if (Current.EnemiesAlive <= 0)
            return;

        Debug.Log("[SystemEncounterService] Player left system while enemies are alive.");

        Defeat(SystemEncounterDefeatReason.PlayerLeftSystem);
    }

    public bool TryResolvePendingReward()
    {
        if (Current == null)
            return false;

        if (Current.State != SystemEncounterState.VictoryPendingReward)
            return false;

        Current.State = SystemEncounterState.Resolved;

        LogCustom("Encounter resolved.");

        _eventBus.Publish(new SystemEncounterResolvedEvent(
            Current.EncounterId,
            Current.SystemId));

        PublishStateChanged();

        return true;
    }

    public void ClearEncounter()
    {
        Current = null;

        LogCustom("Encounter cleared.");
    }

    public void DebugPrintCurrentState()
    {
        if (Current == null)
        {
            LogCustom("Current: null");
            return;
        }

        LogCustom("Current State\n" +
            $"EncounterId: {Current.EncounterId}\n" +
            $"SystemId: {Current.SystemId}\n" +
            $"State: {Current.State}\n" +
            $"EnemiesAlive: {Current.EnemiesAlive}\n" +
            $"AlliesAlive: {Current.AlliesAlive}\n" +
            $"PlayerKills: {Current.PlayerKills}\n" +
            $"PlayerDestroyed: {Current.PlayerDestroyed}\n" +
            $"PlayerIsOnPlanet: {Current.PlayerIsOnPlanet}\n" +
            $"PlanetDays: {Current.DaysPlayerStayedOnPlanetDuringEncounter}\n" +
            $"DefeatReason: {Current.DefeatReason}"
        );
    }

    private void CheckVictory()
    {
        if (!HasActiveEncounter)
            return;

        if (Current.EnemiesAlive > 0)
            return;

        if (Current.PlayerKills <= 0)
            return;

        Current.State = SystemEncounterState.VictoryPendingReward;

        Debug.Log("[SystemEncounterService] Victory pending reward.");

        _eventBus.Publish(new SystemEncounterVictoryPendingRewardEvent(
            Current.EncounterId,
            Current.SystemId,
            Current.PlayerKills));

        PublishStateChanged();
    }

    private void CheckAlliesDestroyedDefeat()
    {
        if (!HasActiveEncounter)
            return;

        if (Current.EnemiesAlive <= 0)
            return;

        if (Current.AlliesAlive > 0)
            return;

        if (!Current.PlayerIsOnPlanet)
            return;

        if (Current.DaysPlayerStayedOnPlanetDuringEncounter < PlanetStayDefeatDays)
            return;

        Defeat(SystemEncounterDefeatReason.AlliesDestroyedWhilePlayerStayedOnPlanet);
    }

    private void Defeat(SystemEncounterDefeatReason reason)
    {
        if (Current == null)
            return;

        if (Current.State != SystemEncounterState.Active)
            return;

        Current.State = SystemEncounterState.Defeated;
        Current.DefeatReason = reason;

        Debug.Log($"[SystemEncounterService] Encounter defeated. Reason: {reason}");

        _eventBus.Publish(new SystemEncounterDefeatedEvent(
            Current.EncounterId,
            Current.SystemId,
            reason));

        PublishStateChanged();
    }

    private void PublishStateChanged()
    {
        if (Current == null)
            return;

        _eventBus.Publish(new SystemEncounterStateChangedEvent(
            Current.EncounterId,
            Current.SystemId,
            Current.State,
            Current.DefeatReason));
    }

    public void RestoreEncounter(ActiveSystemEncounter encounter)
    {
        Current = encounter;

        if (Current == null)
        {
            Debug.Log("[SystemEncounterService] Restore: no encounter.");
            return;
        }

        Debug.Log(
            $"[SystemEncounterService] Restored. Encounter: {Current.EncounterId}, " +
            $"System: {Current.SystemId}, State: {Current.State}, " +
            $"Enemies: {Current.EnemiesAlive}, Allies: {Current.AlliesAlive}"
        );

        PublishStateChanged();
    }
}
