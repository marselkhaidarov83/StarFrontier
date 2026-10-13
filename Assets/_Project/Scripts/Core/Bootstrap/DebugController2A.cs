using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class DebugController2A : CustomMonoBehaviour
{
    private const int ForcedInvasionResolveAfterTicks = 3;

    [Header("War / Forced Invasion")]
    [SerializeField] private StarSystemConfig debugTargetSystem;
    [SerializeField] private bool useCurrentSystemAsTargetIfEmpty = true;
    [SerializeField] private DebugLogChannel warDebugLogChannel = DebugLogChannel.Population;
    [SerializeField] private bool logWarCandidateDetails = true;

    [Header("War / System Status Debug")]
    [SerializeField]
    private StarSystemStatus debugSystemStatusTarget = StarSystemStatus.Stable;
    [SerializeField] private bool debugCreateRecoveryHookOnLiberation = true;

    [Header("War / Offline Debug")]
    [SerializeField, Min(1)] private int debugOfflineWarTargetTickOffset = 3;
    [SerializeField] private bool debugOfflineWarUseCurrentTickAsBase = true;

    [Header("Debug / Combat Damage")]
    [SerializeField] private string debugCombatTargetRuntimeNpcId;
    [SerializeField, Min(1)] private int debugCombatTargetDamage = 10;
    [SerializeField, Min(1)] private int debugCombatPlayerDamage = 10;
    [SerializeField, Min(0.1f)] private float debugNpcOfflineStepHours = 1f;

    [Header("Debug / NPC Stress")]
    [SerializeField, Min(1)] private int debugNpcStressSpawnAttempts = 25;
    [SerializeField, Min(1)] private int debugNpcStressSpawnWaves = 1;
    [SerializeField] private bool debugNpcStressSpawnAllies = true;
    [SerializeField] private bool debugNpcStressSpawnEnemyGroups = true;

    [Header("Debug / Player")]
    [SerializeField] private DebugConfig debugConfig;

    private IServiceRegistry ServiceRegistry =>
        Bootstrapper.Instance != null
            ? Bootstrapper.Instance.ServiceRegistry
            : null;

    [ContextMenu("STAR FRONTIER/War/Force Invasion")]
    private void DebugForceInvasion()
    {
        LogWarDebug("Force invasion command started.");

        if (!TryGetWarDebugServices(
                out IInvasionService invasionService,
                out IConfigService configService,
                out IGameSessionService gameSessionService))
        {
            LogWarDebug("Force invasion stopped: required services are unavailable.");
            return;
        }

        StarSystemConfig targetSystem = ResolveTargetSystem(configService);

        if (targetSystem == null)
        {
            LogWarDebug("Force invasion stopped: target system is not assigned or resolved.");
            return;
        }

        StarSystemConfig sourceSystem = ResolveSourceSystem(targetSystem, configService);

        if (sourceSystem == null)
        {
            LogWarDebug("Force invasion stopped: source system was not resolved. Target=" + targetSystem.Id);
            return;
        }

        int galaxyLevel = GetCurrentGalaxyLevel(gameSessionService);

        LogWarDebug(
            "Force invasion resolved base data. " +
            "Source=" + sourceSystem.Id +
            ", Target=" + targetSystem.Id +
            ", GalaxyLevel=" + galaxyLevel);

        if (!TryPickRandomInvasionCandidate(
                targetSystem,
                galaxyLevel,
                out EnemyGroupSpawnRuleConfig invasionRule,
                out string factionId))
        {
            LogWarDebug(
                "Force invasion stopped: no valid invasion candidate. " +
                "Target=" + targetSystem.Id +
                ", GalaxyLevel=" + galaxyLevel);

            return;
        }

        LogWarDebug(
            "Force invasion picked candidate. " +
            "Rule=" + invasionRule.Id +
            ", Faction=" + factionId);

        bool started =
            invasionService.TryStartInvasion(
                sourceSystem,
                targetSystem,
                invasionRule,
                factionId,
                ForcedInvasionResolveAfterTicks,
                out InvasionState invasionState);

        if (!started || invasionState == null)
        {
            LogWarDebug(
                "Force invasion blocked by InvasionService validation. " +
                "Source=" + sourceSystem.Id +
                ", Target=" + targetSystem.Id +
                ", Faction=" + factionId +
                ", Rule=" + invasionRule.Id);

            return;
        }

        LogWarDebug(
            "Force invasion started. " +
            "InvasionId=" + invasionState.InvasionId +
            ", Source=" + invasionState.SourceSystemId +
            ", Target=" + invasionState.TargetSystemId +
            ", Faction=" + invasionState.FactionId +
            ", Rule=" + invasionRule.Id +
            ", Level=" + invasionState.Level);
    }

    [ContextMenu("STAR FRONTIER/Kill/Kill All NPCs")]
    private void DebugKillAllNpcs()
    {
        if (ServiceRegistry == null)
        {
            LogCustom("[DebugController2A] ServiceRegistry is not initialized.");
            return;
        }

        if (!ServiceRegistry.TryGet(out ISystemNpcRuntimeService npcRuntimeService))
        {
            LogCustom("[DebugController2A] ISystemNpcRuntimeService is not registered.");
            return;
        }

        int npcCount = npcRuntimeService.Npcs != null ? npcRuntimeService.Npcs.Count : 0;
        npcRuntimeService.ClearAll();

        if (ServiceRegistry.TryGet(out ISystemNpcPopulationService populationService))
            populationService.ClearRuntimeState();

        LogCustom("[DebugController2A] Debug Kill All NPCs completed. Removed NPCs: " + npcCount);
    }

    [ContextMenu("STAR FRONTIER/Kill/Kill All NPCs In This System")]
    private void DebugKillAllNpcsInThisSystem()
    {
        if (!TryGetNpcRuntimeAndSession(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IGameSessionService gameSessionService))
        {
            return;
        }

        string currentSystemId = gameSessionService.State.Player.CurrentSystemId;

        if (string.IsNullOrWhiteSpace(currentSystemId))
        {
            LogCustom("[DebugController2A] Current system id is empty.");
            return;
        }

        List<string> npcIdsToRemove = new();

        IReadOnlyList<SystemNpcRuntimeState> npcs = npcRuntimeService.Npcs;

        if (npcs != null)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                SystemNpcRuntimeState npc = npcs[i];

                if (npc == null ||
                    npc.CurrentSystemId != currentSystemId ||
                    string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
                {
                    continue;
                }

                npcIdsToRemove.Add(npc.RuntimeNpcId);
            }
        }

        for (int i = 0; i < npcIdsToRemove.Count; i++)
            npcRuntimeService.DespawnNpc(npcIdsToRemove[i]);

        LogCustom(
            "[DebugController2A] Debug Kill All NPCs In This System completed. " +
            "System: " + currentSystemId +
            ", Removed NPCs: " + npcIdsToRemove.Count);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Spawn Enemy Attack Group In Current System")]
    private void DebugSpawnEnemyAttackGroupInCurrentSystem()
    {
        if (!TryGetPopulationService(out ISystemNpcPopulationService populationService))
            return;

        bool spawned = populationService.DebugSpawnEnemyAttackGroupInCurrentSystem();

        LogCustom("[DebugController2A] Debug Spawn Enemy Attack Group In Current System result: " + spawned);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Spawn Ally Ranger In Current System")]
    private void DebugSpawnAllyRangerInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Ranger);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Spawn Ally Military In Current System")]
    private void DebugSpawnAllyMilitaryInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Military);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Spawn Ally Trader In Current System")]
    private void DebugSpawnAllyTraderInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Trader);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Spawn Ally Science In Current System")]
    private void DebugSpawnAllyScienceInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Science);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Spawn Ally Medic In Current System")]
    private void DebugSpawnAllyMedicInCurrentSystem()
    {
        DebugSpawnAllyInCurrentSystem(AllyRole2A.Medic);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Stress Spawn NPC")]
    private void DebugStressSpawnNpcs()
    {
        if (!TryGetDebugNpcStressServices(
                out ISystemNpcPopulationService populationService,
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        if (!debugNpcStressSpawnAllies && !debugNpcStressSpawnEnemyGroups)
        {
            DebugCombatWarning("[DebugController2A] NPC stress spawn skipped. No spawn type is enabled.");
            return;
        }

        int beforeCount = GetDebugNpcRuntimeCount(npcRuntimeService);
        int spawnedCommands = 0;
        int failedCommands = 0;

        int waveCount = Mathf.Max(1, debugNpcStressSpawnWaves);
        int attemptsPerWave = Mathf.Max(1, debugNpcStressSpawnAttempts);

        for (int wave = 0; wave < waveCount; wave++)
        {
            RunDebugNpcStressSpawnWave(
                populationService,
                attemptsPerWave,
                ref spawnedCommands,
                ref failedCommands);
        }

        int afterCount = GetDebugNpcRuntimeCount(npcRuntimeService);

        DebugCombatLog(
            "[DebugController2A] NPC Stress Spawn completed. " +
            "Waves: " + waveCount +
            ", AttemptsPerWave: " + attemptsPerWave +
            ", SuccessfulCommands: " + spawnedCommands +
            ", FailedCommands: " + failedCommands +
            ", NpcCountBefore: " + beforeCount +
            ", NpcCountAfter: " + afterCount +
            ", Delta: " + (afterCount - beforeCount));
    }

    [ContextMenu("STAR FRONTIER/Spawn/Stress Spawn NPC Wave")]
    private void DebugStressSpawnNpcWave()
    {
        if (!TryGetDebugNpcStressServices(
                out ISystemNpcPopulationService populationService,
                out ISystemNpcRuntimeService npcRuntimeService))
        {
            return;
        }

        int beforeCount = GetDebugNpcRuntimeCount(npcRuntimeService);
        int spawnedCommands = 0;
        int failedCommands = 0;

        RunDebugNpcStressSpawnWave(
            populationService,
            Mathf.Max(1, debugNpcStressSpawnAttempts),
            ref spawnedCommands,
            ref failedCommands);

        int afterCount = GetDebugNpcRuntimeCount(npcRuntimeService);

        DebugCombatLog(
            "[DebugController2A] NPC Stress Spawn Wave completed. " +
            "Attempts: " + Mathf.Max(1, debugNpcStressSpawnAttempts) +
            ", SuccessfulCommands: " + spawnedCommands +
            ", FailedCommands: " + failedCommands +
            ", NpcCountBefore: " + beforeCount +
            ", NpcCountAfter: " + afterCount +
            ", Delta: " + (afterCount - beforeCount));
    }

    [ContextMenu("STAR FRONTIER/State/Print NPC Runtime Count")]
    private void DebugPrintNpcRuntimeCount()
    {
        if (!TryGetDebugNpcRuntimeServiceWithoutTarget(out ISystemNpcRuntimeService npcRuntimeService))
            return;

        DebugCombatLog("[DebugController2A] NPC Runtime Count: " + GetDebugNpcRuntimeCount(npcRuntimeService));
    }

    [ContextMenu("STAR FRONTIER/Damage/Damage First Enemy In Current System")]
    private void DebugDamageFirstEnemyInCurrentSystem()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        string runtimeNpcId = FindFirstAliveEnemyRuntimeIdInCurrentSystem(npcRuntimeService, configService);

        if (string.IsNullOrWhiteSpace(runtimeNpcId))
        {
            DebugCombatWarning("[DebugController2A] Debug damage target failed. No alive enemy was found in current system.");
            return;
        }

        DamageDebugTarget(npcRuntimeService, runtimeNpcId, debugCombatTargetDamage);
    }

    [ContextMenu("STAR FRONTIER/Damage/Damage Target NPC By Runtime ID")]
    private void DebugDamageTargetNpcByRuntimeId()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            DebugCombatWarning("[DebugController2A] debugCombatTargetRuntimeNpcId is empty.");
            return;
        }

        DamageDebugTarget(npcRuntimeService, debugCombatTargetRuntimeNpcId, debugCombatTargetDamage);
    }

    [ContextMenu("STAR FRONTIER/Damage/Damage Player")]
    private void DebugDamagePlayer()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        ShipRuntimeData activeShipBefore = GetDebugActiveShip();

        if (activeShipBefore == null)
        {
            DebugCombatWarning("[DebugController2A] Debug Damage Player failed. Active ship is null.");
            return;
        }

        int shieldBefore = activeShipBefore.CurrentShield;
        int hullBefore = activeShipBefore.CurrentHull;

        playerCombatTargetService.ApplyDamage(Mathf.Max(1, debugCombatPlayerDamage));

        ShipRuntimeData activeShipAfter = GetDebugActiveShip();

        if (activeShipAfter == null)
        {
            DebugCombatWarning("[DebugController2A] Debug Damage Player finished, but active ship is null after damage.");
            return;
        }

        DebugCombatLog(
            "[DebugController2A] Debug Damage Player requested. " +
            "Damage: " + Mathf.Max(1, debugCombatPlayerDamage) +
            ", Shield: " + shieldBefore + " -> " + activeShipAfter.CurrentShield +
            ", Hull: " + hullBefore + " -> " + activeShipAfter.CurrentHull);
    }

    [ContextMenu("STAR FRONTIER/Kill/ First Enemy In Current System")]
    private void DebugKillFirstEnemyInCurrentSystem()
    {
        if (!TryGetDebugCombatServices(
                out ISystemNpcRuntimeService npcRuntimeService,
                out IPlayerCombatTargetService playerCombatTargetService,
                out IConfigService configService))
        {
            return;
        }

        string runtimeNpcId = FindFirstAliveEnemyRuntimeIdInCurrentSystem(npcRuntimeService, configService);

        if (string.IsNullOrWhiteSpace(runtimeNpcId))
        {
            DebugCombatWarning("[DebugController2A] Debug kill enemy failed. No alive enemy was found in current system.");
            return;
        }

        KillDebugEnemy(npcRuntimeService, runtimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Kill/Kill Target NPC By Runtime ID")]
    private void DebugKillTargetNpcByRuntimeId()
    {
        if (!TryGetDebugNpcRuntimeService(out ISystemNpcRuntimeService npcRuntimeService))
            return;

        bool killed = npcRuntimeService.KillNpc(debugCombatTargetRuntimeNpcId, true);

        DebugCombatLog(
            "[DebugController2A] Debug Kill Target NPC result: " +
            killed +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Spawn/Despawn Target NPC By Runtime ID")]
    private void DebugDespawnTargetNpcByRuntimeId()
    {
        if (!TryGetDebugNpcRuntimeService(out ISystemNpcRuntimeService npcRuntimeService))
            return;

        bool despawned = npcRuntimeService.DespawnNpc(debugCombatTargetRuntimeNpcId);

        DebugCombatLog(
            "[DebugController2A] Debug Despawn Target NPC result: " +
            despawned +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Other/Reset Target NPC By Runtime ID")]
    private void DebugResetTargetNpcByRuntimeId()
    {
        if (!TryGetDebugNpcRuntimeService(out ISystemNpcRuntimeService npcRuntimeService))
            return;

        bool reset = npcRuntimeService.ResetNpc(debugCombatTargetRuntimeNpcId);

        DebugCombatLog(
            "[DebugController2A] Debug Reset Target NPC result: " +
            reset +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Other/Force Target NPC Route To Linked System")]
    private void DebugForceTargetNpcRouteToLinkedSystem()
    {
        if (!TryGetDebugNpcOfflineServices(
                out ISystemNpcOfflineRelocationService offlineRelocationService,
                out IGameSessionService gameSessionService))
        {
            return;
        }

        bool forced =
            offlineRelocationService.DebugForceTargetNpcRoute(
                debugCombatTargetRuntimeNpcId,
                gameSessionService.State);

        DebugCombatLog(
            "[DebugController2A] Debug Force Target NPC Route result: " +
            forced +
            ", RuntimeNpcId: " +
            debugCombatTargetRuntimeNpcId);
    }

    [ContextMenu("STAR FRONTIER/Other/Run NPC Offline Step")]
    private void DebugRunNpcOfflineStep()
    {
        if (!TryGetDebugNpcOfflineServices(
                out ISystemNpcOfflineRelocationService offlineRelocationService,
                out IGameSessionService gameSessionService))
        {
            return;
        }

        bool moved =
            offlineRelocationService.DebugProcessOfflineStep(
                gameSessionService.State,
                debugNpcOfflineStepHours);

        DebugCombatLog(
            "[DebugController2A] Debug NPC Offline Step result: " +
            moved +
            ", Hours: " +
            debugNpcOfflineStepHours.ToString("0.00"));
    }

    [ContextMenu("STAR FRONTIER/Other/Reset Current Encounter")]
    private void DebugResetCurrentEncounter()
    {
        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[DebugController2A] ServiceRegistry is not initialized.");
            return;
        }

        if (!ServiceRegistry.TryGet(out ISystemEncounterService encounterService) ||
            encounterService == null)
        {
            DebugCombatWarning("[DebugController2A] ISystemEncounterService is not registered.");
            return;
        }

        encounterService.ClearEncounter();

        DebugCombatLog("[DebugController2A] Debug Reset Current Encounter completed.");
    }

    [ContextMenu("STAR FRONTIER/God Mode/Enable Player God Mode")]
    private void DebugEnablePlayerGodMode()
    {
        SetDebugPlayerGodMode(true);
    }

    [ContextMenu("STAR FRONTIER/God Mode/Disable Player God Mode")]
    private void DebugDisablePlayerGodMode()
    {
        SetDebugPlayerGodMode(false);
    }

    [ContextMenu("STAR FRONTIER/State/Print All NPC Debug State")]
    private void DebugPrintAllNpcDebugState()
    {
        if (!TryGetDebugNpcRuntimeServiceWithoutTarget(out ISystemNpcRuntimeService npcRuntimeService))
            return;

        StringBuilder text = new(2048);
        text.AppendLine("[DebugController2A] NPC Debug State");

        if (npcRuntimeService.Npcs == null || npcRuntimeService.Npcs.Count == 0)
        {
            text.AppendLine("NPCs: none");
            DebugCombatLog(text.ToString());
            return;
        }

        for (int i = 0; i < npcRuntimeService.Npcs.Count; i++)
            AppendNpcDebugState(text, npcRuntimeService.Npcs[i], i);

        DebugCombatLog(text.ToString());
    }

    [ContextMenu("STAR FRONTIER/State/Print Target NPC Debug State")]
    private void DebugPrintTargetNpcDebugState()
    {
        if (!TryGetDebugNpcRuntimeService(out ISystemNpcRuntimeService npcRuntimeService))
            return;

        if (!npcRuntimeService.TryGetNpc(debugCombatTargetRuntimeNpcId, out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            DebugCombatWarning("[DebugController2A] Target NPC debug failed. NPC was not found. RuntimeNpcId: " + debugCombatTargetRuntimeNpcId);
            return;
        }

        StringBuilder text = new(1024);
        text.AppendLine("[DebugController2A] Target NPC Debug State");
        AppendNpcDebugState(text, npc, 0);
        DebugCombatLog(text.ToString());
    }

    [ContextMenu("STAR FRONTIER/State/Print Combat Debug State")]
    private void DebugPrintCombatState()
    {
        StringBuilder text = new(512);

        text.AppendLine("[DebugController2A] Combat Debug State");

        AppendDebugEncounterState(text);
        AppendDebugPlayerState(text);
        AppendDebugTargetState(text);
        AppendDebugNpcCombatState(text);

        DebugCombatLog(text.ToString());
    }

    private bool TryPickRandomInvasionCandidate(
        StarSystemConfig targetSystem,
        int galaxyLevel,
        out EnemyGroupSpawnRuleConfig selectedRule,
        out string selectedFactionId)
    {
        selectedRule = null;
        selectedFactionId = string.Empty;

        List<WarInvasionDebugCandidate> candidates = new();

        if (targetSystem == null)
        {
            LogWarDebug("Candidate scan failed: targetSystem is null.");
            return false;
        }

        if (targetSystem.SystemPopulationRule == null)
        {
            LogWarDebug("Candidate scan failed: SystemPopulationRule is null. Target=" + targetSystem.Id);
            return false;
        }

        if (targetSystem.SystemPopulationRule.EnemyGroupSpawnRuleEntries == null)
        {
            LogWarDebug("Candidate scan failed: EnemyGroupSpawnRuleEntries is null. Target=" + targetSystem.Id);
            return false;
        }

        SystemPopulationEnemyGroupRuleEntry[] entries =
            targetSystem.SystemPopulationRule.EnemyGroupSpawnRuleEntries;

        LogWarDebug(
            "Candidate scan started. " +
            "Target=" + targetSystem.Id +
            ", GalaxyLevel=" + galaxyLevel +
            ", Entries=" + entries.Length);

        for (int i = 0; i < entries.Length; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry = entries[i];

            if (entry == null)
            {
                LogWarCandidate("Entry #" + i + " rejected: entry is null.");
                continue;
            }

            if (!entry.IsValid())
            {
                LogWarCandidate("Entry #" + i + " rejected: entry is not valid.");
                continue;
            }

            EnemyGroupSpawnRuleConfig rule = entry.EnemyGroupSpawnRule;

            if (rule == null)
            {
                LogWarCandidate("Entry #" + i + " rejected: rule is null.");
                continue;
            }

            if (!rule.HasValidEnemiesForGalaxyLevel(galaxyLevel))
            {
                LogWarCandidate(
                    "Rule rejected: no valid enemies for galaxy level. " +
                    "Rule=" + rule.Id +
                    ", GalaxyLevel=" + galaxyLevel);

                continue;
            }

            LogWarCandidate(
                "Rule accepted for faction scan. " +
                "Rule=" + rule.Id +
                ", Weight=" + entry.Weight +
                ", GalaxyLevel=" + galaxyLevel);

            AddFactionCandidates(
                candidates,
                rule,
                galaxyLevel);
        }

        LogWarDebug("Candidate scan completed. Candidates=" + candidates.Count);

        if (candidates.Count == 0)
            return false;

        WarInvasionDebugCandidate candidate =
            candidates[Random.Range(0, candidates.Count)];

        selectedRule = candidate.Rule;
        selectedFactionId = candidate.FactionId;

        return selectedRule != null &&
               !string.IsNullOrWhiteSpace(selectedFactionId);
    }

    private void AddFactionCandidates(
        List<WarInvasionDebugCandidate> candidates,
        EnemyGroupSpawnRuleConfig rule,
        int galaxyLevel)
    {
        IReadOnlyList<EnemyGroupEntryConfig> enemies =
            rule.PickEnemiesForGalaxyLevel(galaxyLevel);

        if (enemies == null || enemies.Count == 0)
        {
            LogWarCandidate(
                "Rule faction scan rejected: PickEnemiesForGalaxyLevel returned empty. " +
                "Rule=" + rule.Id +
                ", GalaxyLevel=" + galaxyLevel);

            return;
        }

        HashSet<string> addedFactionIds = new();

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGroupEntryConfig entry = enemies[i];

            if (entry == null)
            {
                LogWarCandidate("Enemy entry rejected: entry is null. Rule=" + rule.Id);
                continue;
            }

            if (entry.EnemyConfig == null)
            {
                LogWarCandidate("Enemy entry rejected: EnemyConfig is null. Rule=" + rule.Id);
                continue;
            }

            if (entry.EnemyConfig.WeaponGroups == null ||
                entry.EnemyConfig.WeaponGroups.Count == 0)
            {
                LogWarCandidate(
                    "Enemy entry rejected: WeaponGroups are empty. " +
                    "Rule=" + rule.Id +
                    ", Enemy=" + entry.EnemyConfig.Id);

                continue;
            }

            IReadOnlyList<WeaponGroupConfig> weaponGroups =
                entry.EnemyConfig.WeaponGroups;

            for (int j = 0; j < weaponGroups.Count; j++)
            {
                WeaponGroupConfig weaponGroup = weaponGroups[j];

                if (weaponGroup == null)
                {
                    LogWarCandidate(
                        "Weapon group rejected: null. " +
                        "Rule=" + rule.Id +
                        ", Enemy=" + entry.EnemyConfig.Id);

                    continue;
                }

                string factionId =
                    ConvertFactionToId(weaponGroup.EnemyFaction);

                if (string.IsNullOrWhiteSpace(factionId) ||
                    factionId == "unknown")
                {
                    LogWarCandidate(
                        "Weapon group rejected: unknown faction. " +
                        "Rule=" + rule.Id +
                        ", Enemy=" + entry.EnemyConfig.Id +
                        ", WeaponGroup=" + weaponGroup.Id +
                        ", EnemyFaction=" + weaponGroup.EnemyFaction);

                    continue;
                }

                if (!addedFactionIds.Add(factionId))
                    continue;

                candidates.Add(
                    new WarInvasionDebugCandidate
                    {
                        Rule = rule,
                        FactionId = factionId
                    });

                LogWarCandidate(
                    "Candidate added. " +
                    "Rule=" + rule.Id +
                    ", Enemy=" + entry.EnemyConfig.Id +
                    ", WeaponGroup=" + weaponGroup.Id +
                    ", Faction=" + factionId);
            }
        }
    }

    private void LogWarDebug(string message)
    {
        if (Bootstrapper.Instance == null)
            return;

        if (!Bootstrapper.Instance.IsDebugLogEnabled(warDebugLogChannel))
            return;

        Bootstrapper.Instance.LogDebug(
            warDebugLogChannel,
            "[DebugController2A][War] " + message);
    }

    private void LogWarCandidate(string message)
    {
        if (!logWarCandidateDetails)
            return;

        LogWarDebug(message);
    }

    private StarSystemConfig ResolveTargetSystem(IConfigService configService)
    {
        if (debugTargetSystem != null)
            return debugTargetSystem;

        if (!useCurrentSystemAsTargetIfEmpty)
            return null;

        return configService.GetCurrentSystemConfig();
    }

    private StarSystemConfig ResolveSourceSystem(
        StarSystemConfig targetSystem,
        IConfigService configService)
    {
        if (targetSystem == null)
            return null;

        if (targetSystem.LinkedSystems != null)
        {
            for (int i = 0; i < targetSystem.LinkedSystems.Length; i++)
            {
                StarSystemLink link = targetSystem.LinkedSystems[i];

                if (link == null ||
                    link.LinkedSystem == null ||
                    link.LinkedSystem.Id == targetSystem.Id)
                {
                    continue;
                }

                return link.LinkedSystem;
            }
        }

        IReadOnlyList<StarSystemConfig> allSystems = configService.GetAllStarSystems();

        if (allSystems == null)
            return null;

        for (int i = 0; i < allSystems.Count; i++)
        {
            StarSystemConfig system = allSystems[i];

            if (system != null && system.Id != targetSystem.Id)
                return system;
        }

        return null;
    }

    private int GetCurrentGalaxyLevel(IGameSessionService gameSessionService)
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.OverrideNpcGalaxyLevel)
        {
            return Bootstrapper.Instance.DebugNpcGalaxyLevel;
        }

        if (gameSessionService == null ||
            !gameSessionService.HasActiveSession ||
            gameSessionService.State == null ||
            gameSessionService.State.Galaxy == null ||
            gameSessionService.State.Galaxy.Sectors == null)
        {
            return 1;
        }

        int unlockedSectorCount = 0;

        for (int i = 0; i < gameSessionService.State.Galaxy.Sectors.Count; i++)
        {
            SectorRuntimeState sector = gameSessionService.State.Galaxy.Sectors[i];

            if (sector != null && sector.IsUnlocked)
                unlockedSectorCount++;
        }

        return Mathf.Clamp(Mathf.Max(1, unlockedSectorCount), 1, 10);
    }

    private string ConvertFactionToId(WeaponGroupEnemyFaction faction)
    {
        switch (faction)
        {
            case WeaponGroupEnemyFaction.AI:
                return "ai";

            case WeaponGroupEnemyFaction.Ancients:
                return "ancients";

            case WeaponGroupEnemyFaction.Infected:
                return "infected";

            default:
                return "unknown";
        }
    }

    private bool TryGetWarDebugServices(
        out IInvasionService invasionService,
        out IConfigService configService,
        out IGameSessionService gameSessionService)
    {
        invasionService = null;
        configService = null;
        gameSessionService = null;

        if (ServiceRegistry == null)
        {
            Debug.LogWarning("[DebugController2A] Bootstrapper or ServiceRegistry is not ready.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out invasionService) || invasionService == null)
        {
            Debug.LogWarning("[DebugController2A] IInvasionService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out configService) || configService == null)
        {
            Debug.LogWarning("[DebugController2A] IConfigService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null)
        {
            Debug.LogWarning("[DebugController2A] IGameSessionService state is unavailable.");
            return false;
        }

        return true;
    }

    private void DebugSpawnAllyInCurrentSystem(AllyRole2A role)
    {
        if (!TryGetPopulationService(out ISystemNpcPopulationService populationService))
            return;

        bool spawned = populationService.DebugSpawnAllyInCurrentSystem(role);

        LogCustom(
            "[DebugController2A] Debug Spawn Ally In Current System result: " +
            spawned +
            ", Role: " +
            role);
    }

    private bool TryGetPopulationService(out ISystemNpcPopulationService populationService)
    {
        populationService = null;

        if (ServiceRegistry == null)
        {
            LogCustom("[DebugController2A] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out populationService) ||
            populationService == null)
        {
            LogCustom("[DebugController2A] ISystemNpcPopulationService is not registered.");
            return false;
        }

        return true;
    }

    private bool TryGetNpcRuntimeAndSession(
        out ISystemNpcRuntimeService npcRuntimeService,
        out IGameSessionService gameSessionService)
    {
        npcRuntimeService = null;
        gameSessionService = null;

        if (ServiceRegistry == null)
        {
            LogCustom("[DebugController2A] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out npcRuntimeService) ||
            npcRuntimeService == null)
        {
            LogCustom("[DebugController2A] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null ||
            gameSessionService.State.Player == null)
        {
            LogCustom("[DebugController2A] IGameSessionService state is unavailable.");
            return false;
        }

        return true;
    }

    private bool TryGetDebugNpcRuntimeService(out ISystemNpcRuntimeService npcRuntimeService)
    {
        npcRuntimeService = null;

        if (!TryGetDebugNpcRuntimeServiceWithoutTarget(out npcRuntimeService))
            return false;

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            DebugCombatWarning("[DebugController2A] debugCombatTargetRuntimeNpcId is empty.");
            return false;
        }

        return true;
    }

    private bool TryGetDebugNpcRuntimeServiceWithoutTarget(out ISystemNpcRuntimeService npcRuntimeService)
    {
        npcRuntimeService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[DebugController2A] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out npcRuntimeService) ||
            npcRuntimeService == null)
        {
            DebugCombatWarning("[DebugController2A] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        return true;
    }

    private bool TryGetDebugCombatServices(
        out ISystemNpcRuntimeService npcRuntimeService,
        out IPlayerCombatTargetService playerCombatTargetService,
        out IConfigService configService)
    {
        npcRuntimeService = null;
        playerCombatTargetService = null;
        configService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[DebugController2A] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out npcRuntimeService) || npcRuntimeService == null)
        {
            DebugCombatWarning("[DebugController2A] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out playerCombatTargetService) || playerCombatTargetService == null)
        {
            DebugCombatWarning("[DebugController2A] IPlayerCombatTargetService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out configService) || configService == null)
        {
            DebugCombatWarning("[DebugController2A] IConfigService is not registered.");
            return false;
        }

        return true;
    }

    private bool TryGetDebugNpcOfflineServices(
        out ISystemNpcOfflineRelocationService offlineRelocationService,
        out IGameSessionService gameSessionService)
    {
        offlineRelocationService = null;
        gameSessionService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[DebugController2A] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out offlineRelocationService) ||
            offlineRelocationService == null)
        {
            DebugCombatWarning("[DebugController2A] ISystemNpcOfflineRelocationService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null)
        {
            DebugCombatWarning("[DebugController2A] IGameSessionService state is unavailable.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            DebugCombatWarning("[DebugController2A] debugCombatTargetRuntimeNpcId is empty.");
            return false;
        }

        return true;
    }

    private bool TryGetDebugNpcStressServices(
        out ISystemNpcPopulationService populationService,
        out ISystemNpcRuntimeService npcRuntimeService)
    {
        populationService = null;
        npcRuntimeService = null;

        if (ServiceRegistry == null)
        {
            DebugCombatWarning("[DebugController2A] ServiceRegistry is not initialized.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out populationService) || populationService == null)
        {
            DebugCombatWarning("[DebugController2A] ISystemNpcPopulationService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out npcRuntimeService) || npcRuntimeService == null)
        {
            DebugCombatWarning("[DebugController2A] ISystemNpcRuntimeService is not registered.");
            return false;
        }

        return true;
    }

    private void RunDebugNpcStressSpawnWave(
        ISystemNpcPopulationService populationService,
        int attempts,
        ref int spawnedCommands,
        ref int failedCommands)
    {
        AllyRole2A[] allyRoles =
        {
            AllyRole2A.Ranger,
            AllyRole2A.Military,
            AllyRole2A.Trader,
            AllyRole2A.Science,
            AllyRole2A.Medic
        };

        for (int i = 0; i < attempts; i++)
        {
            bool spawned;

            if (debugNpcStressSpawnAllies && debugNpcStressSpawnEnemyGroups)
            {
                spawned = i % 2 == 0
                    ? populationService.DebugSpawnAllyInCurrentSystem(allyRoles[i % allyRoles.Length])
                    : populationService.DebugSpawnEnemyAttackGroupInCurrentSystem();
            }
            else if (debugNpcStressSpawnAllies)
            {
                spawned = populationService.DebugSpawnAllyInCurrentSystem(allyRoles[i % allyRoles.Length]);
            }
            else
            {
                spawned = populationService.DebugSpawnEnemyAttackGroupInCurrentSystem();
            }

            if (spawned)
                spawnedCommands++;
            else
                failedCommands++;
        }
    }

    private int GetDebugNpcRuntimeCount(ISystemNpcRuntimeService npcRuntimeService)
    {
        if (npcRuntimeService == null || npcRuntimeService.Npcs == null)
            return 0;

        return npcRuntimeService.Npcs.Count;
    }

    private string FindFirstAliveEnemyRuntimeIdInCurrentSystem(
        ISystemNpcRuntimeService npcRuntimeService,
        IConfigService configService)
    {
        StarSystemConfig currentSystem = configService.GetCurrentSystemConfig();

        if (currentSystem == null || string.IsNullOrWhiteSpace(currentSystem.Id))
            return null;

        IReadOnlyList<SystemNpcRuntimeState> enemies =
            npcRuntimeService.GetAliveNpcsInSystemByType(
                currentSystem.Id,
                SystemNpcType.Enemy);

        if (enemies == null || enemies.Count == 0)
            return null;

        for (int i = 0; i < enemies.Count; i++)
        {
            SystemNpcRuntimeState enemy = enemies[i];

            if (enemy != null &&
                enemy.IsAlive &&
                enemy.LifeState == SystemNpcLifeState.Alive)
            {
                return enemy.RuntimeNpcId;
            }
        }

        return null;
    }

    private void DamageDebugTarget(
        ISystemNpcRuntimeService npcRuntimeService,
        string runtimeNpcId,
        int damage)
    {
        if (!npcRuntimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            DebugCombatWarning("[DebugController2A] Debug damage target failed. NPC was not found. RuntimeNpcId: " + runtimeNpcId);
            return;
        }

        if (!npc.IsAlive || npc.LifeState != SystemNpcLifeState.Alive)
        {
            DebugCombatWarning("[DebugController2A] Debug damage target failed. NPC is not alive. RuntimeNpcId: " + runtimeNpcId);
            return;
        }

        int safeDamage = Mathf.Max(1, damage);

        npcRuntimeService.ApplyDamage(runtimeNpcId, safeDamage, true, true);

        DebugCombatLog(
            "[DebugController2A] Debug Damage Target completed. " +
            "RuntimeNpcId: " + runtimeNpcId +
            ", Damage: " + safeDamage);
    }

    private void KillDebugEnemy(
        ISystemNpcRuntimeService npcRuntimeService,
        string runtimeNpcId)
    {
        if (!npcRuntimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            DebugCombatWarning("[DebugController2A] Debug kill enemy failed. NPC was not found. RuntimeNpcId: " + runtimeNpcId);
            return;
        }

        if (!npc.IsEnemy)
        {
            DebugCombatWarning(
                "[DebugController2A] Debug kill enemy failed. NPC is not an enemy. " +
                "RuntimeNpcId: " + runtimeNpcId +
                ", Type: " + npc.NpcType);

            return;
        }

        if (!npc.IsAlive || npc.LifeState != SystemNpcLifeState.Alive)
        {
            DebugCombatWarning("[DebugController2A] Debug kill enemy failed. Enemy is not alive. RuntimeNpcId: " + runtimeNpcId);
            return;
        }

        int lethalDamage = Mathf.Max(npc.CurrentHull + npc.CurrentShield, 999999);

        npcRuntimeService.ApplyDamage(runtimeNpcId, lethalDamage, true, true);

        DebugCombatLog(
            "[DebugController2A] Debug Kill Enemy completed. " +
            "RuntimeNpcId: " + runtimeNpcId +
            ", Damage: " + lethalDamage);
    }

    private ShipRuntimeData GetDebugActiveShip()
    {
        if (ServiceRegistry == null)
            return null;

        if (!ServiceRegistry.TryGet(out IGameSessionService gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null ||
            gameSessionService.State.Player == null)
        {
            return null;
        }

        return gameSessionService.State.Player.GetActiveShip();
    }

    private ShipStats GetDebugActiveShipStats()
    {
        if (ServiceRegistry == null)
            return null;

        if (!ServiceRegistry.TryGet(out IHangarService hangarService) ||
            hangarService == null)
        {
            return null;
        }

        return hangarService.GetActiveShipStats();
    }

    private void SetDebugPlayerGodMode(bool enabled)
    {
        if (debugConfig == null)
        {
            DebugCombatWarning("[DebugController2A] DebugConfig is not assigned.");
            return;
        }

        debugConfig.enableGodMode = enabled;

        DebugCombatLog("[DebugController2A] Player God Mode: " + enabled);
    }

    private void AppendDebugEncounterState(StringBuilder text)
    {
        if (ServiceRegistry == null)
        {
            text.AppendLine("Encounter: ServiceRegistry unavailable");
            return;
        }

        if (!ServiceRegistry.TryGet(out ISystemEncounterService encounterService) ||
            encounterService == null)
        {
            text.AppendLine("Encounter: service unavailable");
            return;
        }

        ActiveSystemEncounter encounter = encounterService.Current;

        if (encounter == null)
        {
            text.AppendLine("Encounter: none");
            return;
        }

        text.Append("Encounter: ")
            .Append(encounter.EncounterId)
            .Append(", System: ")
            .Append(encounter.SystemId)
            .Append(", State: ")
            .Append(encounter.State)
            .Append(", EnemiesAlive: ")
            .Append(encounter.EnemiesAlive)
            .Append(", AlliesAlive: ")
            .Append(encounter.AlliesAlive)
            .Append(", PlayerKills: ")
            .Append(encounter.PlayerKills)
            .Append(", DefeatReason: ")
            .AppendLine(encounter.DefeatReason.ToString());
    }

    private void AppendDebugPlayerState(StringBuilder text)
    {
        ShipRuntimeData activeShip = GetDebugActiveShip();

        if (activeShip == null)
        {
            text.AppendLine("Player: active ship unavailable");
            return;
        }

        ShipStats stats = GetDebugActiveShipStats();

        int maxHull = stats != null ? stats.MaxHull : activeShip.HullCapacity;
        int maxShield = stats != null ? stats.MaxShield : activeShip.CurrentShield;

        text.Append("Player: ShipId: ")
            .Append(activeShip.ShipId)
            .Append(", Hull: ")
            .Append(activeShip.CurrentHull)
            .Append(" / ")
            .Append(maxHull)
            .Append(", Shield: ")
            .Append(activeShip.CurrentShield)
            .Append(" / ")
            .Append(maxShield)
            .Append(", Energy: ")
            .Append(activeShip.CurrentEnergy)
            .AppendLine();
    }

    private void AppendDebugTargetState(StringBuilder text)
    {
        if (ServiceRegistry == null)
        {
            text.AppendLine("Target: ServiceRegistry unavailable");
            return;
        }

        if (ServiceRegistry.TryGet(out IPlayerAttackService playerAttackService) &&
            playerAttackService != null &&
            !string.IsNullOrWhiteSpace(playerAttackService.CurrentTargetNpcId))
        {
            AppendDebugNpcTarget(text, "PlayerAttack target", playerAttackService.CurrentTargetNpcId);
            return;
        }

        if (ServiceRegistry.TryGet(out ITargetService2A targetService) &&
            targetService != null &&
            targetService.State != null &&
            targetService.State.HasTarget)
        {
            text.Append("Targeting target: ")
                .Append(targetService.State.CurrentTargetId)
                .Append(", Type: ")
                .Append(targetService.State.CurrentTargetType)
                .Append(", Distance: ")
                .Append(targetService.State.CurrentTargetDistance.ToString("0.0"))
                .Append(", InRange: ")
                .Append(targetService.State.IsTargetInRange)
                .AppendLine();

            AppendDebugNpcTarget(text, "Targeting NPC state", targetService.State.CurrentTargetId);
            return;
        }

        text.AppendLine("Target: none");
    }

    private void AppendDebugNpcTarget(
        StringBuilder text,
        string label,
        string runtimeNpcId)
    {
        if (ServiceRegistry == null)
            return;

        if (!ServiceRegistry.TryGet(out ISystemNpcRuntimeService npcRuntimeService) ||
            npcRuntimeService == null)
        {
            text.Append(label).AppendLine(": NPC runtime service unavailable");
            return;
        }

        if (!npcRuntimeService.TryGetNpc(runtimeNpcId, out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            text.Append(label)
                .Append(": not found. RuntimeNpcId: ")
                .AppendLine(runtimeNpcId);

            return;
        }

        text.Append(label)
            .Append(": ")
            .Append(npc.DisplayName)
            .Append(", RuntimeNpcId: ")
            .Append(npc.RuntimeNpcId)
            .Append(", Type: ")
            .Append(npc.NpcType)
            .Append(", Hull: ")
            .Append(npc.CurrentHull)
            .Append(" / ")
            .Append(npc.MaxHull)
            .Append(", Shield: ")
            .Append(npc.CurrentShield)
            .Append(" / ")
            .Append(npc.MaxShield)
            .Append(", CombatState: ")
            .Append(npc.CombatState)
            .Append(", Alive: ")
            .Append(npc.IsAlive)
            .AppendLine();
    }

    private void AppendDebugNpcCombatState(StringBuilder text)
    {
        if (ServiceRegistry == null)
        {
            text.AppendLine("NPC Debug: ServiceRegistry unavailable");
            return;
        }

        if (!ServiceRegistry.TryGet(out ISystemNpcRuntimeService npcRuntimeService) ||
            npcRuntimeService == null)
        {
            text.AppendLine("NPC Debug: runtime service unavailable");
            return;
        }

        if (string.IsNullOrWhiteSpace(debugCombatTargetRuntimeNpcId))
        {
            text.AppendLine("NPC Debug: target RuntimeNpcId is empty");
            return;
        }

        if (!npcRuntimeService.TryGetNpc(debugCombatTargetRuntimeNpcId, out SystemNpcRuntimeState npc) ||
            npc == null)
        {
            text.Append("NPC Debug: target not found. RuntimeNpcId: ")
                .AppendLine(debugCombatTargetRuntimeNpcId);

            return;
        }

        AppendNpcDebugState(text, npc, 0);
    }

    private void AppendNpcDebugState(
        StringBuilder text,
        SystemNpcRuntimeState npc,
        int index)
    {
        if (npc == null)
        {
            text.Append("#").Append(index).AppendLine(": null NPC");
            return;
        }

        text.Append("#")
            .Append(index)
            .Append(": ")
            .Append(npc.DisplayName)
            .Append(", RuntimeNpcId: ")
            .Append(npc.RuntimeNpcId)
            .Append(", Type: ")
            .Append(npc.NpcType)
            .Append(", Role: ")
            .Append(npc.AllyRole)
            .Append(", Faction: ")
            .Append(BuildDebugNpcFaction(npc))
            .AppendLine();

        text.Append("  Route: OriginSystem: ")
            .Append(npc.OriginSystemId)
            .Append(", CurrentSystem: ")
            .Append(npc.CurrentSystemId)
            .Append(", TargetSystem: ")
            .Append(npc.TargetSystemId)
            .Append(", CurrentPlanet: ")
            .Append(npc.CurrentPlanetId)
            .Append(", TargetPlanet: ")
            .Append(npc.TargetPlanetId)
            .Append(", TravelState: ")
            .Append(npc.TravelState)
            .AppendLine();

        text.Append("  Target: CurrentTargetRuntimeNpcId: ")
            .Append(npc.CurrentTargetRuntimeNpcId)
            .Append(", BehaviorTargetRuntimeNpcId: ")
            .Append(npc.BehaviorTargetRuntimeNpcId)
            .AppendLine();

        text.Append("  State: LifeState: ")
            .Append(npc.LifeState)
            .Append(", IsAlive: ")
            .Append(npc.IsAlive)
            .Append(", Behavior: ")
            .Append(npc.CurrentBehavior)
            .Append(", PrevBehavior: ")
            .Append(npc.PrevBehavior)
            .Append(", CombatState: ")
            .Append(npc.CombatState)
            .Append(", IsFighting: ")
            .Append(npc.IsFighting)
            .AppendLine();

        text.Append("  Timers: BehaviorStartedTick: ")
            .Append(npc.BehaviorStartedTick)
            .Append(", BehaviorEndsTick: ")
            .Append(npc.BehaviorEndsTick)
            .Append(", TravelStartTick: ")
            .Append(npc.TravelStartTick)
            .Append(", TravelEndTick: ")
            .Append(npc.TravelEndTick)
            .Append(", DestroyedAtTick: ")
            .Append(npc.DestroyedAtTick)
            .Append(", NextRespawnTick: ")
            .Append(npc.NextRespawnTick)
            .AppendLine();
    }

    private string BuildDebugNpcFaction(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "Unknown";

        if (npc.IsEnemy)
            return "Enemy";

        if (npc.IsPirate)
            return "Pirate";

        if (npc.IsAlly)
            return "Civilization";

        return "Unknown";
    }

    private void DebugCombatLog(string message)
    {
        Debug.unityLogger.Log(LogType.Log, (object)message, this);
    }

    private void DebugCombatWarning(string message)
    {
        Debug.unityLogger.Log(LogType.Warning, (object)message, this);
    }

    private struct WarInvasionDebugCandidate
    {
        public EnemyGroupSpawnRuleConfig Rule;
        public string FactionId;
    }

    [ContextMenu("STAR FRONTIER/War/Status/Apply Inspector Status")]
    private void DebugApplyInspectorSystemStatus()
    {
        DebugApplySystemStatus(debugSystemStatusTarget);
    }

    [ContextMenu("STAR FRONTIER/War/Status/Set Stable")]
    private void DebugSetSystemStable()
    {
        DebugApplySystemStatus(StarSystemStatus.Stable);
    }

    [ContextMenu("STAR FRONTIER/War/Status/Set Threat")]
    private void DebugSetSystemThreat()
    {
        DebugApplySystemStatus(StarSystemStatus.Threat);
    }

    [ContextMenu("STAR FRONTIER/War/Status/Set Invasion")]
    private void DebugSetSystemInvasion()
    {
        DebugApplySystemStatus(StarSystemStatus.Invasion);
    }

    [ContextMenu("STAR FRONTIER/War/Status/Set Captured")]
    private void DebugSetSystemCaptured()
    {
        DebugApplySystemStatus(StarSystemStatus.Captured);
    }

    [ContextMenu("STAR FRONTIER/War/Status/Set Liberated Recovery")]
    private void DebugSetSystemLiberatedRecovery()
    {
        DebugApplySystemStatus(StarSystemStatus.RecoveryReady);
    }

    private void DebugApplySystemStatus(StarSystemStatus requestedStatus)
    {
        LogWarDebug(
            "System status debug command started. " +
            "RequestedStatus=" + requestedStatus);

        if (!TryGetWarStatusDebugServices(
                out ISystemSecurityService systemSecurityService,
                out IConfigService configService,
                out IGameSessionService gameSessionService,
                out IGameTimeService gameTimeService))
        {
            LogWarDebug("System status debug stopped: required services are unavailable.");
            return;
        }

        StarSystemConfig targetSystem =
            ResolveTargetSystem(configService);

        if (targetSystem == null ||
            string.IsNullOrWhiteSpace(targetSystem.Id))
        {
            LogWarDebug("System status debug stopped: target system is not assigned or resolved.");
            return;
        }

        if (!TryFindRuntimeSystemState(
                gameSessionService,
                targetSystem.Id,
                out StarSystemRuntimeState runtimeSystemState))
        {
            LogWarDebug(
                "System status debug stopped: runtime system state was not found. " +
                "SystemId=" + targetSystem.Id);

            return;
        }

        StarSystemStatus previousStatus =
            runtimeSystemState.SystemStatus;

        bool applied;

        switch (requestedStatus)
        {
            case StarSystemStatus.Captured:
                applied =
                    systemSecurityService.CaptureSystem(targetSystem.Id);
                break;

            case StarSystemStatus.RecoveryReady:
                applied =
                    systemSecurityService.LiberateSystemByPlayer(targetSystem.Id);

                if (applied && debugCreateRecoveryHookOnLiberation)
                {
                    systemSecurityService.MarkRecoveryHookPending(
                        targetSystem.Id,
                        gameTimeService.CurrentQuantTick,
                        "debug_liberation");
                }

                break;

            case StarSystemStatus.Stable:
                runtimeSystemState.ClearRecoveryHook();
                applied =
                    systemSecurityService.SetSystemStatus(
                        targetSystem.Id,
                        StarSystemStatus.Stable);
                break;

            case StarSystemStatus.Threat:
                runtimeSystemState.ClearRecoveryHook();
                applied =
                    systemSecurityService.SetSystemStatus(
                        targetSystem.Id,
                        StarSystemStatus.Threat);
                break;

            case StarSystemStatus.Invasion:
                runtimeSystemState.ClearRecoveryHook();
                applied =
                    systemSecurityService.SetSystemStatus(
                        targetSystem.Id,
                        StarSystemStatus.Invasion);
                break;

            default:
                applied =
                    systemSecurityService.SetSystemStatus(
                        targetSystem.Id,
                        requestedStatus);
                break;
        }

        systemSecurityService.TryGetSystemStatus(
            targetSystem.Id,
            out StarSystemStatus finalStatus);

        LogWarDebug(
            "System status debug command completed. " +
            "Applied=" + applied +
            ", SystemId=" + targetSystem.Id +
            ", PreviousStatus=" + previousStatus +
            ", RequestedStatus=" + requestedStatus +
            ", FinalStatus=" + finalStatus +
            ", HasPendingRecoveryHook=" + runtimeSystemState.HasPendingRecoveryHook +
            ", RecoveryHookTick=" + runtimeSystemState.RecoveryHookCreatedAtTick +
            ", RecoveryHookReason=" + runtimeSystemState.RecoveryHookReason +
            ", IsSecured=" + systemSecurityService.IsSystemSecured(targetSystem.Id));
    }

    private bool TryGetWarStatusDebugServices(
    out ISystemSecurityService systemSecurityService,
    out IConfigService configService,
    out IGameSessionService gameSessionService,
    out IGameTimeService gameTimeService)
    {
        systemSecurityService = null;
        configService = null;
        gameSessionService = null;
        gameTimeService = null;

        if (ServiceRegistry == null)
        {
            LogWarDebug("System status debug failed: Bootstrapper or ServiceRegistry is not ready.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out systemSecurityService) ||
            systemSecurityService == null)
        {
            LogWarDebug("System status debug failed: ISystemSecurityService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out configService) ||
            configService == null)
        {
            LogWarDebug("System status debug failed: IConfigService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null)
        {
            LogWarDebug("System status debug failed: IGameSessionService state is unavailable.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out gameTimeService) ||
            gameTimeService == null)
        {
            LogWarDebug("System status debug failed: IGameTimeService is not registered.");
            return false;
        }

        return true;
    }

    private bool TryFindRuntimeSystemState(
    IGameSessionService gameSessionService,
    string systemId,
    out StarSystemRuntimeState runtimeSystemState)
    {
        runtimeSystemState = null;

        if (gameSessionService == null ||
            gameSessionService.State == null ||
            gameSessionService.State.Galaxy == null ||
            gameSessionService.State.Galaxy.Systems == null ||
            string.IsNullOrWhiteSpace(systemId))
        {
            return false;
        }

        for (int i = 0; i < gameSessionService.State.Galaxy.Systems.Count; i++)
        {
            StarSystemRuntimeState candidate =
                gameSessionService.State.Galaxy.Systems[i];

            if (candidate == null)
                continue;

            if (candidate.SystemId != systemId)
                continue;

            runtimeSystemState = candidate;
            return true;
        }

        return false;
    }

    [ContextMenu("STAR FRONTIER/War/Offline/Run War Offline Step")]
    private void DebugRunWarOfflineStep()
    {
        LogWarDebug(
            "War offline debug command started. " +
            "TickOffset=" + debugOfflineWarTargetTickOffset +
            ", UseCurrentTickAsBase=" + debugOfflineWarUseCurrentTickAsBase);

        if (!TryGetWarOfflineDebugServices(
                out IInvasionService invasionService,
                out IGameSessionService gameSessionService,
                out IGameTimeService gameTimeService))
        {
            LogWarDebug("War offline debug stopped: required services are unavailable.");
            return;
        }

        if (gameSessionService.State == null ||
            gameSessionService.State.Galaxy == null)
        {
            LogWarDebug("War offline debug stopped: game state or galaxy state is unavailable.");
            return;
        }

        int baseTick =
            debugOfflineWarUseCurrentTickAsBase
                ? gameTimeService.CurrentQuantTick
                : gameSessionService.State.Galaxy.GalaxyDay;

        baseTick =
            Mathf.Max(
                1,
                baseTick);

        int targetTick =
            Mathf.Max(
                baseTick + Mathf.Max(1, debugOfflineWarTargetTickOffset),
                baseTick + 1);

        int activeBefore =
            invasionService.GetActiveInvasionCount();

        int changedCount =
            invasionService.ProcessOfflineWarCatchUp(
                gameSessionService.State,
                targetTick);

        int activeAfter =
            invasionService.GetActiveInvasionCount();

        LogWarDebug(
            "War offline debug command completed. " +
            "BaseTick=" + baseTick +
            ", TargetTick=" + targetTick +
            ", ChangedCount=" + changedCount +
            ", ActiveInvasionsBefore=" + activeBefore +
            ", ActiveInvasionsAfter=" + activeAfter);
    }

    private bool TryGetWarOfflineDebugServices(
        out IInvasionService invasionService,
        out IGameSessionService gameSessionService,
        out IGameTimeService gameTimeService)
    {
        invasionService = null;
        gameSessionService = null;
        gameTimeService = null;

        if (ServiceRegistry == null)
        {
            LogWarDebug("War offline debug failed: Bootstrapper or ServiceRegistry is not ready.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out invasionService) ||
            invasionService == null)
        {
            LogWarDebug("War offline debug failed: IInvasionService is not registered.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out gameSessionService) ||
            gameSessionService == null ||
            gameSessionService.State == null)
        {
            LogWarDebug("War offline debug failed: IGameSessionService state is unavailable.");
            return false;
        }

        if (!ServiceRegistry.TryGet(out gameTimeService) ||
            gameTimeService == null)
        {
            LogWarDebug("War offline debug failed: IGameTimeService is not registered.");
            return false;
        }

        return true;
    }


}