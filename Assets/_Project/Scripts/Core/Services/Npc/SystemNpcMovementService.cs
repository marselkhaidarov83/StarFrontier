using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcMovementService : CustomService, ISystemNpcMovementService
{
    private enum NpcBehaviorActionKind
    {
        DetermineBehaviorAndTarget = 1,
        ResolveMovementTarget = 2,
        BuildNewRoute = 3,
        RefreshRoute = 4,
        RerollMovementTarget = 5,
        ReuseOldRoute = 6,
        MoveAlongRoute = 7,
        CompleteMovement = 8
    }

    private sealed class NpcBehaviorActionAnalytics
    {
        private sealed class Entry
        {
            public int ResolveTargetGetNextTargetAllyCount;
            public int ResolveTargetGetNextTargetPirateCount;
            public int ResolveTargetGetNextTargetEnemyCount;
            public int ResolveTargetGetNextTargetFallbackCount;

            public int ResolveTargetPlanetCacheCheckCount;
            public int ResolveTargetPlanetCacheHitCount;
            public int ResolveTargetPlanetCacheMissCount;
            public int ResolveTargetPlanetConfigLookupCount;
            public int ResolveTargetPlanetOrbitalPositionCount;
            public int ResolveTargetPlanetInvalidPointCheckCount;

            public double ResolveTargetGetNextTargetAllyMs;
            public double ResolveTargetGetNextTargetPirateMs;
            public double ResolveTargetGetNextTargetEnemyMs;
            public double ResolveTargetGetNextTargetFallbackMs;

            public double ResolveTargetPlanetCacheCheckMs;
            public double ResolveTargetPlanetConfigLookupMs;
            public double ResolveTargetPlanetOrbitalPositionMs;
            public double ResolveTargetPlanetInvalidPointCheckMs;

            public int ResolveTargetGetNextTargetCount;
            public int ResolveTargetCombatNpcCheckCount;
            public int ResolveTargetSystemExitCheckCount;
            public int ResolveTargetPlanetLookupCount;
            public int ResolveTargetTargetPositionCheckCount;
            public int ResolveTargetFallbackCount;

            public double ResolveTargetGetNextTargetMs;
            public double ResolveTargetCombatNpcCheckMs;
            public double ResolveTargetSystemExitCheckMs;
            public double ResolveTargetPlanetLookupMs;
            public double ResolveTargetTargetPositionCheckMs;
            public double ResolveTargetFallbackMs;
            public double ResolveTargetBoundaryMs;
            public double ResolveTargetArrivalBeforeBuildMs;
            public double ResolveTargetActiveRouteLookupMs;

            public SystemNpcBehaviorType Behavior;
            public int NpcCount;
            public int DetermineBehaviorAndTargetCount;
            public int ResolveMovementTargetCount;
            public int BuildNewRouteCount;
            public int RefreshRouteCount;
            public int RerollMovementTargetCount;
            public int ReuseOldRouteCount;
            public int MoveAlongRouteCount;
            public int CompleteMovementCount;

            public double DetermineBehaviorAndTargetMs;
            public double ResolveMovementTargetMs;
            public double BuildNewRouteMs;
            public double RefreshRouteMs;
            public double RerollMovementTargetMs;
            public double ReuseOldRouteMs;
            public double MoveAlongRouteMs;
            public double CompleteMovementMs;

            public int BuildRouteInitialLimitCount;
            public int BuildRouteBuildRouteCount;
            public int BuildRouteCreateRouteStateCount;
            public int BuildRouteUpdateTargetCount;
            public int BuildRouteFallbackCount;
            public int BuildRoutePathPointCountTotal;
            public int BuildRouteMaxPathPointCount;

            public double BuildRouteInitialLimitMs;
            public double BuildRouteBuildRouteMs;
            public double BuildRouteCreateRouteStateMs;
            public double BuildRouteUpdateTargetMs;
            public double BuildRouteFallbackMs;

            public int RefreshRouteInitialLimitCount;
            public int RefreshRouteBuildRouteCount;
            public int RefreshRouteCreateRouteStateCount;
            public int RefreshRouteUpdateTargetCount;
            public int RefreshRouteFallbackCount;
            public int RefreshRoutePathPointCountTotal;
            public int RefreshRouteMaxPathPointCount;

            public double RefreshRouteInitialLimitMs;
            public double RefreshRouteBuildRouteMs;
            public double RefreshRouteCreateRouteStateMs;
            public double RefreshRouteUpdateTargetMs;
            public double RefreshRouteFallbackMs;

            public int ReuseRouteChecksCount;
            public int ReuseRouteUpdateTargetCount;

            public double ReuseRouteChecksMs;
            public double ReuseRouteUpdateTargetMs;

            public int MoveRouteLookupCount;
            public int MoveStartTurnCount;
            public int MoveArrivalThresholdCount;
            public int MovePathLengthCount;
            public int MoveDistanceMathCount;
            public int MovePointOnPathCount;
            public int MoveDirectionOnPathCount;
            public int MoveApplyStateCount;
            public int MovePublishCount;
            public int MoveArrivalCheckCount;
            public int MoveClearRouteCount;
            public int MovePathPointCountTotal;
            public int MoveMaxPathPointCount;

            public double MoveRouteLookupMs;
            public double MoveStartTurnMs;
            public double MoveArrivalThresholdMs;
            public double MovePathLengthMs;
            public double MoveDistanceMathMs;
            public double MovePointOnPathMs;
            public double MoveDirectionOnPathMs;
            public double MoveApplyStateMs;
            public double MovePublishMs;
            public double MoveArrivalCheckMs;
            public double MoveClearRouteMs;

            public int CompletePrecheckCount;
            public int CompleteClearRouteCount;
            public int CompleteAssignPositionCount;
            public int CompleteStateSwitchCount;
            public int CompleteSystemTravelCount;
            public int CompleteLogCount;
            public int CompletePublishEventCount;
            public int CompleteBehaviorCount;
            public int CompleteTotalCount;
            public int CompleteBlockedCount;
            public int CompleteSystemTravelCaseCount;

            public double CompletePrecheckMs;
            public double CompleteClearRouteMs;
            public double CompleteAssignPositionMs;
            public double CompleteStateSwitchMs;
            public double CompleteSystemTravelMs;
            public double CompleteLogMs;
            public double CompletePublishEventMs;
            public double CompleteBehaviorMs;
            public double CompleteTotalMs;
        }

        public void AddRouteBuildDetails(
            SystemNpcBehaviorType behavior,
            NpcDetailedMovementPhaseStats phaseStats,
            NpcBehaviorActionKind routeActionKind)
        {
            if (phaseStats == null)
                return;

            Entry entry =
                GetOrCreateEntry(behavior);

            bool isRefresh =
                routeActionKind == NpcBehaviorActionKind.RefreshRoute;

            if (isRefresh)
            {
                AddDetail(
                    ref entry.RefreshRouteInitialLimitCount,
                    ref entry.RefreshRouteInitialLimitMs,
                    phaseStats.EnsureInitialLimitMs);

                AddDetail(
                    ref entry.RefreshRouteBuildRouteCount,
                    ref entry.RefreshRouteBuildRouteMs,
                    phaseStats.EnsureBuildRouteMs);

                AddDetail(
                    ref entry.RefreshRouteCreateRouteStateCount,
                    ref entry.RefreshRouteCreateRouteStateMs,
                    phaseStats.EnsureCreateRouteStateMs);

                AddDetail(
                    ref entry.RefreshRouteUpdateTargetCount,
                    ref entry.RefreshRouteUpdateTargetMs,
                    phaseStats.EnsureUpdateTargetMs);

                AddDetail(
                    ref entry.RefreshRouteFallbackCount,
                    ref entry.RefreshRouteFallbackMs,
                    phaseStats.EnsureFallbackMs);

                entry.RefreshRoutePathPointCountTotal += phaseStats.EnsureBuiltRoutePathCount;

                if (phaseStats.EnsureBuiltRoutePathCount > entry.RefreshRouteMaxPathPointCount)
                    entry.RefreshRouteMaxPathPointCount = phaseStats.EnsureBuiltRoutePathCount;

                return;
            }

            AddDetail(
                ref entry.BuildRouteInitialLimitCount,
                ref entry.BuildRouteInitialLimitMs,
                phaseStats.EnsureInitialLimitMs);

            AddDetail(
                ref entry.BuildRouteBuildRouteCount,
                ref entry.BuildRouteBuildRouteMs,
                phaseStats.EnsureBuildRouteMs);

            AddDetail(
                ref entry.BuildRouteCreateRouteStateCount,
                ref entry.BuildRouteCreateRouteStateMs,
                phaseStats.EnsureCreateRouteStateMs);

            AddDetail(
                ref entry.BuildRouteUpdateTargetCount,
                ref entry.BuildRouteUpdateTargetMs,
                phaseStats.EnsureUpdateTargetMs);

            AddDetail(
                ref entry.BuildRouteFallbackCount,
                ref entry.BuildRouteFallbackMs,
                phaseStats.EnsureFallbackMs);

            entry.BuildRoutePathPointCountTotal += phaseStats.EnsureBuiltRoutePathCount;

            if (phaseStats.EnsureBuiltRoutePathCount > entry.BuildRouteMaxPathPointCount)
                entry.BuildRouteMaxPathPointCount = phaseStats.EnsureBuiltRoutePathCount;
        }

        public void AddRouteReuseDetails(
            SystemNpcBehaviorType behavior,
            NpcDetailedMovementPhaseStats phaseStats)
        {
            if (phaseStats == null)
                return;

            Entry entry =
                GetOrCreateEntry(behavior);

            AddDetail(
                ref entry.ReuseRouteChecksCount,
                ref entry.ReuseRouteChecksMs,
                phaseStats.EnsureReuseChecksMs);

            AddDetail(
                ref entry.ReuseRouteUpdateTargetCount,
                ref entry.ReuseRouteUpdateTargetMs,
                phaseStats.EnsureUpdateTargetMs);
        }

        public void AddMoveAlongRouteDetails(
            SystemNpcBehaviorType behavior,
            NpcDetailedMovementPhaseStats phaseStats)
        {
            if (phaseStats == null)
                return;

            Entry entry =
                GetOrCreateEntry(behavior);

            AddDetail(ref entry.MoveRouteLookupCount, ref entry.MoveRouteLookupMs, phaseStats.RouteLookupMs);
            AddDetail(ref entry.MoveStartTurnCount, ref entry.MoveStartTurnMs, phaseStats.StartTurnMs);
            AddDetail(ref entry.MoveArrivalThresholdCount, ref entry.MoveArrivalThresholdMs, phaseStats.ArrivalThresholdMs);
            AddDetail(ref entry.MovePathLengthCount, ref entry.MovePathLengthMs, phaseStats.PathLengthMs);
            AddDetail(ref entry.MoveDistanceMathCount, ref entry.MoveDistanceMathMs, phaseStats.DistanceMathMs);
            AddDetail(ref entry.MovePointOnPathCount, ref entry.MovePointOnPathMs, phaseStats.PointOnPathMs);
            AddDetail(ref entry.MoveDirectionOnPathCount, ref entry.MoveDirectionOnPathMs, phaseStats.DirectionOnPathMs);
            AddDetail(ref entry.MoveApplyStateCount, ref entry.MoveApplyStateMs, phaseStats.ApplyStateMs);
            AddDetail(ref entry.MovePublishCount, ref entry.MovePublishMs, phaseStats.PublishMs);
            AddDetail(ref entry.MoveArrivalCheckCount, ref entry.MoveArrivalCheckMs, phaseStats.ArrivalCheckMs);
            AddDetail(ref entry.MoveClearRouteCount, ref entry.MoveClearRouteMs, phaseStats.ClearRouteMs);

            entry.MovePathPointCountTotal += phaseStats.PathPointCountTotal;

            if (phaseStats.MaxPathPointCount > entry.MoveMaxPathPointCount)
                entry.MoveMaxPathPointCount = phaseStats.MaxPathPointCount;
        }

        public void AddCompleteMovementDetails(
            SystemNpcBehaviorType behavior,
            NpcDetailedMovementPhaseStats phaseStats)
        {
            if (phaseStats == null)
                return;

            Entry entry =
                GetOrCreateEntry(behavior);

            AddDetail(ref entry.CompletePrecheckCount, ref entry.CompletePrecheckMs, phaseStats.CompletePrecheckMs);
            AddDetail(ref entry.CompleteClearRouteCount, ref entry.CompleteClearRouteMs, phaseStats.CompleteClearRouteMs);
            AddDetail(ref entry.CompleteAssignPositionCount, ref entry.CompleteAssignPositionMs, phaseStats.CompleteAssignPositionMs);
            AddDetail(ref entry.CompleteStateSwitchCount, ref entry.CompleteStateSwitchMs, phaseStats.CompleteStateSwitchMs);
            AddDetail(ref entry.CompleteSystemTravelCount, ref entry.CompleteSystemTravelMs, phaseStats.CompleteSystemTravelMs);
            AddDetail(ref entry.CompleteLogCount, ref entry.CompleteLogMs, phaseStats.CompleteLogMs);
            AddDetail(ref entry.CompletePublishEventCount, ref entry.CompletePublishEventMs, phaseStats.CompletePublishEventMs);
            AddDetail(ref entry.CompleteBehaviorCount, ref entry.CompleteBehaviorMs, phaseStats.CompleteBehaviorMs);
            AddDetail(ref entry.CompleteTotalCount, ref entry.CompleteTotalMs, phaseStats.CompleteTotalMs);

            if (phaseStats.CompleteWasBlocked)
                entry.CompleteBlockedCount++;

            if (phaseStats.CompleteWasSystemTravel)
                entry.CompleteSystemTravelCaseCount++;
        }

        private static void AddDetail(
            ref int count,
            ref double totalMs,
            double elapsedMs)
        {
            if (elapsedMs <= 0d)
                return;

            count++;
            totalMs += elapsedMs;
        }

        public void AddResolveTargetDetails(
            SystemNpcBehaviorType behavior,
            SystemNpcMovementTargetResolveStats stats,
            double boundaryMs,
            double arrivalBeforeBuildMs,
            double activeRouteLookupMs)
        {
            Entry entry =
                GetOrCreateEntry(behavior);

            if (stats != null)
            {
                entry.ResolveTargetGetNextTargetCount += stats.GetNextTargetCount;
                entry.ResolveTargetCombatNpcCheckCount += stats.CombatNpcCheckCount;
                entry.ResolveTargetSystemExitCheckCount += stats.SystemExitCheckCount;
                entry.ResolveTargetPlanetLookupCount += stats.PlanetLookupCount;
                entry.ResolveTargetTargetPositionCheckCount += stats.TargetPositionCheckCount;
                entry.ResolveTargetFallbackCount += stats.FallbackCount;

                entry.ResolveTargetGetNextTargetMs += stats.GetNextTargetMs;
                entry.ResolveTargetCombatNpcCheckMs += stats.CombatNpcCheckMs;
                entry.ResolveTargetSystemExitCheckMs += stats.SystemExitCheckMs;
                entry.ResolveTargetPlanetLookupMs += stats.PlanetLookupMs;
                entry.ResolveTargetTargetPositionCheckMs += stats.TargetPositionCheckMs;
                entry.ResolveTargetFallbackMs += stats.FallbackMs;

                entry.ResolveTargetGetNextTargetAllyCount += stats.GetNextTargetAllyCount;
                entry.ResolveTargetGetNextTargetPirateCount += stats.GetNextTargetPirateCount;
                entry.ResolveTargetGetNextTargetEnemyCount += stats.GetNextTargetEnemyCount;
                entry.ResolveTargetGetNextTargetFallbackCount += stats.GetNextTargetFallbackCount;

                entry.ResolveTargetPlanetCacheCheckCount += stats.PlanetCacheCheckCount;
                entry.ResolveTargetPlanetCacheHitCount += stats.PlanetCacheHitCount;
                entry.ResolveTargetPlanetCacheMissCount += stats.PlanetCacheMissCount;
                entry.ResolveTargetPlanetConfigLookupCount += stats.PlanetConfigLookupCount;
                entry.ResolveTargetPlanetOrbitalPositionCount += stats.PlanetOrbitalPositionCount;
                entry.ResolveTargetPlanetInvalidPointCheckCount += stats.PlanetInvalidPointCheckCount;

                entry.ResolveTargetGetNextTargetAllyMs += stats.GetNextTargetAllyMs;
                entry.ResolveTargetGetNextTargetPirateMs += stats.GetNextTargetPirateMs;
                entry.ResolveTargetGetNextTargetEnemyMs += stats.GetNextTargetEnemyMs;
                entry.ResolveTargetGetNextTargetFallbackMs += stats.GetNextTargetFallbackMs;

                entry.ResolveTargetPlanetCacheCheckMs += stats.PlanetCacheCheckMs;
                entry.ResolveTargetPlanetConfigLookupMs += stats.PlanetConfigLookupMs;
                entry.ResolveTargetPlanetOrbitalPositionMs += stats.PlanetOrbitalPositionMs;
                entry.ResolveTargetPlanetInvalidPointCheckMs += stats.PlanetInvalidPointCheckMs;
            }

            entry.ResolveTargetBoundaryMs += boundaryMs;
            entry.ResolveTargetArrivalBeforeBuildMs += arrivalBeforeBuildMs;
            entry.ResolveTargetActiveRouteLookupMs += activeRouteLookupMs;
        }

        private readonly Dictionary<SystemNpcBehaviorType, Entry> _entries =
            new Dictionary<SystemNpcBehaviorType, Entry>();

        public void Reset()
        {
            _entries.Clear();
        }

        public void AddNpc(SystemNpcBehaviorType behavior)
        {
            GetOrCreateEntry(behavior).NpcCount++;
        }

        public void Add(
            SystemNpcBehaviorType behavior,
            NpcBehaviorActionKind actionKind,
            double elapsedMs)
        {
            if (elapsedMs <= 0d)
                return;

            Entry entry =
                GetOrCreateEntry(behavior);

            switch (actionKind)
            {
                case NpcBehaviorActionKind.DetermineBehaviorAndTarget:
                    entry.DetermineBehaviorAndTargetMs += elapsedMs;
                    entry.DetermineBehaviorAndTargetCount++;
                    break;

                case NpcBehaviorActionKind.ResolveMovementTarget:
                    entry.ResolveMovementTargetMs += elapsedMs;
                    entry.ResolveMovementTargetCount++;
                    break;

                case NpcBehaviorActionKind.BuildNewRoute:
                    entry.BuildNewRouteMs += elapsedMs;
                    entry.BuildNewRouteCount++;
                    break;

                case NpcBehaviorActionKind.RefreshRoute:
                    entry.RefreshRouteMs += elapsedMs;
                    entry.RefreshRouteCount++;
                    break;

                case NpcBehaviorActionKind.RerollMovementTarget:
                    entry.RerollMovementTargetMs += elapsedMs;
                    entry.RerollMovementTargetCount++;
                    break;

                case NpcBehaviorActionKind.ReuseOldRoute:
                    entry.ReuseOldRouteMs += elapsedMs;
                    entry.ReuseOldRouteCount++;
                    break;

                case NpcBehaviorActionKind.MoveAlongRoute:
                    entry.MoveAlongRouteMs += elapsedMs;
                    entry.MoveAlongRouteCount++;
                    break;

                case NpcBehaviorActionKind.CompleteMovement:
                    entry.CompleteMovementMs += elapsedMs;
                    entry.CompleteMovementCount++;
                    break;
            }
        }

        public string BuildLogFields()
        {
            StringBuilder builder =
                new StringBuilder();

            foreach (KeyValuePair<SystemNpcBehaviorType, Entry> pair in _entries)
            {
                Entry entry =
                    pair.Value;

                if (entry == null)
                    continue;

                builder.Append(" | Behavior=");
                builder.Append(entry.Behavior);
                builder.Append(",Npcs=");
                builder.Append(entry.NpcCount);

                AppendAction(builder, "1_DetermineBehaviorAndTarget", entry.DetermineBehaviorAndTargetCount, entry.DetermineBehaviorAndTargetMs);
                AppendAction(builder, "2_ResolveMovementTarget", entry.ResolveMovementTargetCount, entry.ResolveMovementTargetMs);

                AppendAction(builder, "2a_ResolveTarget_GetNextTarget", entry.ResolveTargetGetNextTargetCount, entry.ResolveTargetGetNextTargetMs);
                AppendAction(builder, "2a1_ResolveTarget_GetNextTargetAlly", entry.ResolveTargetGetNextTargetAllyCount, entry.ResolveTargetGetNextTargetAllyMs);
                AppendAction(builder, "2a2_ResolveTarget_GetNextTargetPirate", entry.ResolveTargetGetNextTargetPirateCount, entry.ResolveTargetGetNextTargetPirateMs);
                AppendAction(builder, "2a3_ResolveTarget_GetNextTargetEnemy", entry.ResolveTargetGetNextTargetEnemyCount, entry.ResolveTargetGetNextTargetEnemyMs);
                AppendAction(builder, "2a4_ResolveTarget_GetNextTargetFallback", entry.ResolveTargetGetNextTargetFallbackCount, entry.ResolveTargetGetNextTargetFallbackMs);
                AppendAction(builder, "2b_ResolveTarget_CombatNpcCheck", entry.ResolveTargetCombatNpcCheckCount, entry.ResolveTargetCombatNpcCheckMs);
                AppendAction(builder, "2c_ResolveTarget_SystemExitCheck", entry.ResolveTargetSystemExitCheckCount, entry.ResolveTargetSystemExitCheckMs);
                AppendAction(builder, "2d_ResolveTarget_PlanetLookup", entry.ResolveTargetPlanetLookupCount, entry.ResolveTargetPlanetLookupMs);
                AppendAction(builder, "2d1_ResolveTarget_PlanetCacheCheck", entry.ResolveTargetPlanetCacheCheckCount, entry.ResolveTargetPlanetCacheCheckMs);
                AppendAction(builder, "2d2_ResolveTarget_PlanetCacheHit", entry.ResolveTargetPlanetCacheHitCount, 0d);
                AppendAction(builder, "2d3_ResolveTarget_PlanetCacheMiss", entry.ResolveTargetPlanetCacheMissCount, 0d);
                AppendAction(builder, "2d4_ResolveTarget_PlanetConfigLookup", entry.ResolveTargetPlanetConfigLookupCount, entry.ResolveTargetPlanetConfigLookupMs);
                AppendAction(builder, "2d5_ResolveTarget_PlanetOrbitalPosition", entry.ResolveTargetPlanetOrbitalPositionCount, entry.ResolveTargetPlanetOrbitalPositionMs);
                AppendAction(builder, "2d6_ResolveTarget_PlanetInvalidPointCheck", entry.ResolveTargetPlanetInvalidPointCheckCount, entry.ResolveTargetPlanetInvalidPointCheckMs);
                AppendAction(builder, "2e_ResolveTarget_TargetPositionCheck", entry.ResolveTargetTargetPositionCheckCount, entry.ResolveTargetTargetPositionCheckMs);
                AppendAction(builder, "2f_ResolveTarget_Fallback", entry.ResolveTargetFallbackCount, entry.ResolveTargetFallbackMs);
                AppendAction(builder, "2g_ResolveTarget_Boundary", entry.ResolveMovementTargetCount, entry.ResolveTargetBoundaryMs);
                AppendAction(builder, "2h_ResolveTarget_ArrivalBeforeBuild", entry.ResolveMovementTargetCount, entry.ResolveTargetArrivalBeforeBuildMs);
                AppendAction(builder, "2i_ResolveTarget_ActiveRouteLookup", entry.ResolveMovementTargetCount, entry.ResolveTargetActiveRouteLookupMs);

                AppendAction(builder, "3_BuildNewRoute", entry.BuildNewRouteCount, entry.BuildNewRouteMs);
                AppendAction(builder, "3a_BuildRoute_InitialLimit", entry.BuildRouteInitialLimitCount, entry.BuildRouteInitialLimitMs);
                AppendAction(builder, "3b_BuildRoute_BuildPath", entry.BuildRouteBuildRouteCount, entry.BuildRouteBuildRouteMs);
                AppendAction(builder, "3c_BuildRoute_CreateRouteState", entry.BuildRouteCreateRouteStateCount, entry.BuildRouteCreateRouteStateMs);
                AppendAction(builder, "3d_BuildRoute_UpdateTarget", entry.BuildRouteUpdateTargetCount, entry.BuildRouteUpdateTargetMs);
                AppendAction(builder, "3e_BuildRoute_Fallback", entry.BuildRouteFallbackCount, entry.BuildRouteFallbackMs);
                AppendRoutePointStats(builder, "3f_BuildRoute_PathPoints", entry.BuildRoutePathPointCountTotal, entry.BuildRouteMaxPathPointCount);

                AppendAction(builder, "4_RefreshRoute", entry.RefreshRouteCount, entry.RefreshRouteMs);
                AppendAction(builder, "4a_RefreshRoute_InitialLimit", entry.RefreshRouteInitialLimitCount, entry.RefreshRouteInitialLimitMs);
                AppendAction(builder, "4b_RefreshRoute_BuildPath", entry.RefreshRouteBuildRouteCount, entry.RefreshRouteBuildRouteMs);
                AppendAction(builder, "4c_RefreshRoute_CreateRouteState", entry.RefreshRouteCreateRouteStateCount, entry.RefreshRouteCreateRouteStateMs);
                AppendAction(builder, "4d_RefreshRoute_UpdateTarget", entry.RefreshRouteUpdateTargetCount, entry.RefreshRouteUpdateTargetMs);
                AppendAction(builder, "4e_RefreshRoute_Fallback", entry.RefreshRouteFallbackCount, entry.RefreshRouteFallbackMs);
                AppendRoutePointStats(builder, "4f_RefreshRoute_PathPoints", entry.RefreshRoutePathPointCountTotal, entry.RefreshRouteMaxPathPointCount);

                AppendAction(builder, "5_RerollMovementTarget", entry.RerollMovementTargetCount, entry.RerollMovementTargetMs);

                AppendAction(builder, "6_ReuseOldRoute", entry.ReuseOldRouteCount, entry.ReuseOldRouteMs);
                AppendAction(builder, "6a_ReuseOldRoute_Checks", entry.ReuseRouteChecksCount, entry.ReuseRouteChecksMs);
                AppendAction(builder, "6b_ReuseOldRoute_UpdateTarget", entry.ReuseRouteUpdateTargetCount, entry.ReuseRouteUpdateTargetMs);

                AppendAction(builder, "7_MoveAlongRoute", entry.MoveAlongRouteCount, entry.MoveAlongRouteMs);
                AppendAction(builder, "7a_Move_RouteLookup", entry.MoveRouteLookupCount, entry.MoveRouteLookupMs);
                AppendAction(builder, "7b_Move_StartTurn", entry.MoveStartTurnCount, entry.MoveStartTurnMs);
                AppendAction(builder, "7c_Move_ArrivalThreshold", entry.MoveArrivalThresholdCount, entry.MoveArrivalThresholdMs);
                AppendAction(builder, "7d_Move_PathLength", entry.MovePathLengthCount, entry.MovePathLengthMs);
                AppendAction(builder, "7e_Move_DistanceMath", entry.MoveDistanceMathCount, entry.MoveDistanceMathMs);
                AppendAction(builder, "7f_Move_PointOnPath", entry.MovePointOnPathCount, entry.MovePointOnPathMs);
                AppendAction(builder, "7g_Move_DirectionOnPath", entry.MoveDirectionOnPathCount, entry.MoveDirectionOnPathMs);
                AppendAction(builder, "7h_Move_ApplyState", entry.MoveApplyStateCount, entry.MoveApplyStateMs);
                AppendAction(builder, "7i_Move_Publish", entry.MovePublishCount, entry.MovePublishMs);
                AppendAction(builder, "7j_Move_ArrivalCheck", entry.MoveArrivalCheckCount, entry.MoveArrivalCheckMs);
                AppendAction(builder, "7k_Move_ClearRoute", entry.MoveClearRouteCount, entry.MoveClearRouteMs);
                AppendRoutePointStats(builder, "7l_Move_PathPoints", entry.MovePathPointCountTotal, entry.MoveMaxPathPointCount);

                AppendAction(builder, "8_CompleteMovement", entry.CompleteMovementCount, entry.CompleteMovementMs);
                AppendAction(builder, "8a_Complete_Precheck", entry.CompletePrecheckCount, entry.CompletePrecheckMs);
                AppendAction(builder, "8b_Complete_ClearRoute", entry.CompleteClearRouteCount, entry.CompleteClearRouteMs);
                AppendAction(builder, "8c_Complete_AssignPosition", entry.CompleteAssignPositionCount, entry.CompleteAssignPositionMs);
                AppendAction(builder, "8d_Complete_StateSwitch", entry.CompleteStateSwitchCount, entry.CompleteStateSwitchMs);
                AppendAction(builder, "8e_Complete_SystemTravel", entry.CompleteSystemTravelCount, entry.CompleteSystemTravelMs);
                AppendAction(builder, "8f_Complete_Log", entry.CompleteLogCount, entry.CompleteLogMs);
                AppendAction(builder, "8g_Complete_PublishEvent", entry.CompletePublishEventCount, entry.CompletePublishEventMs);
                AppendAction(builder, "8h_Complete_Behavior", entry.CompleteBehaviorCount, entry.CompleteBehaviorMs);
                AppendAction(builder, "8i_Complete_Total", entry.CompleteTotalCount, entry.CompleteTotalMs);

                builder.Append(" | 8j_Complete_BlockedCount=");
                builder.Append(entry.CompleteBlockedCount);
                builder.Append(" | 8k_Complete_SystemTravelCaseCount=");
                builder.Append(entry.CompleteSystemTravelCaseCount);
            }

            return builder.ToString();
        }

        private static void AppendRoutePointStats(
            StringBuilder builder,
            string name,
            int totalPathPoints,
            int maxPathPoints)
        {
            builder.Append(" | ");
            builder.Append(name);
            builder.Append("Total=");
            builder.Append(totalPathPoints);
            builder.Append(" | ");
            builder.Append(name);
            builder.Append("Max=");
            builder.Append(maxPathPoints);
        }

        private Entry GetOrCreateEntry(SystemNpcBehaviorType behavior)
        {
            if (!_entries.TryGetValue(behavior, out Entry entry) ||
                entry == null)
            {
                entry =
                    new Entry
                    {
                        Behavior = behavior
                    };

                _entries[behavior] = entry;
            }

            return entry;
        }

        private static void AppendAction(
            StringBuilder builder,
            string name,
            int count,
            double ms)
        {
            builder.Append(" | ");
            builder.Append(name);
            builder.Append("Count=");
            builder.Append(count);
            builder.Append(" | ");
            builder.Append(name);
            builder.Append("Ms=");
            builder.Append(ms.ToString("F2"));
        }
    }

    private enum NpcRouteBuildBudgetKind
    {
        None,
        NoRouteInSpace,
        FinalPlanetApproach,
        RefreshExistingRoute,
        NoRouteOnPlanet
    }

    private sealed class NpcDetailedMovementPhaseStats
    {
        public double EnsureBudgetEnterResetMs;
        public double EnsureBudgetEnterPlanetLaunchPopulationMs;
        public double EnsureBudgetEnterTotalBudgetMs;
        public double EnsureBudgetEnterRefreshBudgetMs;
        public string EnsureBudgetKind;
        public string EnsureBudgetEnterDecision;
        public double EnsureEntrySnapshotMs;
        public double EnsureArrivalMathMs;
        public double EnsureRouteReuseDistanceMs;
        public double EnsureBudgetCheckMs;
        public double EnsureBudgetEnterMs;
        public double EnsureFallbackMapConfigMs;
        public double EnsureFallbackMapPatrolBoundsMs;
        public double EnsureFallbackMapSetupMs;
        public double EnsureFallbackMapLoopMs;
        public double EnsureFallbackMapRandomPositionMs;
        public double EnsureFallbackMapInvalidPointCheckMs;
        public double EnsureFallbackMapAssignMs;

        public bool EnsureFallbackMapUsedFallback;
        public int EnsureFallbackMapAttemptsConfigured;
        public int EnsureFallbackMapAttemptsChecked;

        public double EnsureFallbackRerollTotalMs;
        public double EnsureFallbackRerollDispatchMs;
        public double EnsureFallbackRerollClearRouteMs;
        public double EnsureFallbackRerollResetStateMs;
        public double EnsureFallbackRerollLogMs;

        public double EnsureFallbackSystemExitConfigMs;
        public double EnsureFallbackSystemExitRouteServiceMs;
        public double EnsureFallbackSystemExitSetupMs;
        public double EnsureFallbackSystemExitLoopMs;
        public double EnsureFallbackSystemExitInvalidPointCheckMs;
        public double EnsureFallbackSystemExitAssignMs;

        public bool EnsureFallbackRerolled;
        public bool EnsureFallbackSystemExitUsedFallback;
        public string EnsureFallbackRerollKind;
        public int EnsureFallbackSystemExitRouteCount;
        public int EnsureFallbackSystemExitAttemptsChecked;

        public double CompleteWrapperTotalMs;
        public double CompleteDelayCheckMs;
        public double CompleteDelayCurrentSystemCheckMs;
        public double CompleteDelayTravelStateCheckMs;
        public double CompleteDelayRouteTargetKindMs;
        public double CompleteDelayCounterMs;
        public double CompleteMovementBodyMs;

        public bool CompleteDelayWasApplied;
        public string CompleteDelayDecision;
        public int CompleteDelayCompletionsUsedBefore;
        public int CompleteDelayCompletionsUsedAfter;
        public int CompleteDelayMaxCompletionsPerTick;

        public double TickInitialDebugLogMs;
        public double TickTargetPositionInitMs;
        public double TickEnsureOuterMs;
        public double TickRouteLookupAndRegisterMs;
        public double TickStartTurnOuterMs;
        public double TickRouteMoveOuterMs;
        public double TickPublishOuterMs;
        public double TickArrivalOrCompleteOuterMs;
        public double EnsureUpdateTargetPrecheckMs;
        public double EnsureUpdateTargetStartTurnMs;
        public double EnsureUpdateTargetPathLengthMs;
        public double EnsureUpdateTargetArrivalThresholdMs;
        public double EnsureUpdateTargetDistanceMathMs;
        public double EnsureUpdateTargetPointOnPathMs;
        public double EnsureUpdateTargetAssignMs;
        public double EnsureFastRouteTotalMs;
        public double EnsureFastRouteLookupMs;
        public double EnsureFastRouteReusableCheckMs;
        public double EnsureFastRouteUpdateTargetMs;
        public double EnsureFastRouteArrivalMathMs;
        public double EnsureFastRouteLogMs;
        public int EnsureFastRoutePathPointCount;
        public double TickPreRouteOrInitialMs;
        public double TickWaitingInitialRouteMs;
        public readonly SystemNpcMovementTargetResolveStats TargetResolveStats =
            new SystemNpcMovementTargetResolveStats();
        public double CompletePrecheckMs;
        public double CompleteClearRouteMs;
        public double CompleteAssignPositionMs;
        public double CompleteStateSwitchMs;
        public double CompleteSystemTravelMs;
        public double CompleteLogMs;
        public double CompletePublishEventMs;
        public double CompleteBehaviorMs;
        public double CompleteTotalMs;

        public bool CompleteWasBlocked;
        public bool CompleteWasSystemTravel;
        public string CompleteRouteTargetKind;
        public string CompleteRouteTargetMoveKind;
        public string CompleteBehaviorBefore;
        public string CompleteTravelStateBefore;
        public string CompleteTravelStateAfter;
        public string CompleteSystemBefore;
        public string CompleteSystemAfter;
        public string CompleteTargetSystemId;
        public string CompleteTargetPlanetId;
        public string CompleteCurrentPlanetBefore;
        public string CompleteCurrentPlanetAfter;
        public double EnsureDirectionMs;
        public double RouteLookupMs;
        public double StartTurnMs;
        public double ArrivalThresholdMs;
        public double PathLengthMs;
        public double DistanceMathMs;
        public double PointOnPathMs;
        public double DirectionOnPathMs;
        public double ApplyStateMs;
        public double PublishMs;
        public double ArrivalCheckMs;
        public double CompleteMs;
        public double ClearRouteMs;

        public double EnsureResolveTargetMs;
        public double EnsureBoundaryMs;
        public double EnsureArrivalBeforeBuildMs;
        public double EnsureActiveRouteLookupMs;
        public double EnsureReuseChecksMs;
        public double EnsureInitialLimitMs;
        public double EnsureBuildRouteMs;
        public double EnsureCreateRouteStateMs;
        public double EnsureUpdateTargetMs;
        public double EnsureFallbackMs;

        public int RouteMissingCount;
        public int ArrivedAfterEnsureDirectionCount;
        public int StartTurnConsumedCount;
        public int CompletedCount;
        public int ClearedRouteCount;
        public int PublishedPositionChangedCount;
        public int PathPointCountTotal;
        public int MaxPathPointCount;

        public string EnsureDecision;
        public string EnsureEntryRouteTargetKind;
        public string EnsureEntryRouteTargetMoveKind;
        public string EnsureExitRouteTargetKind;
        public string EnsureExitRouteTargetMoveKind;
        public bool EnsureEntryIsOnPlanet;
        public bool EnsureEntryWaitingForInitialRouteBuild;
        public bool EnsureEntryReleaseFromPlanetAfterInitialRouteBuild;
        public bool EnsureEntryHasActiveRoute;
        public bool EnsureEntryHasSameContextRoute;
        public bool EnsureEntryRouteWasReusable;
        public bool EnsureBoundaryAdjusted;
        public bool EnsureBuiltRoute;
        public bool EnsureUsedExistingRouteAfterBuildFail;
        public int EnsureEntryRoutePathCount;
        public int EnsureBuiltRoutePathCount;
        public int EnsureInitialRouteBuildsUsedBefore;
        public int EnsureInitialRouteBuildsUsedAfter;
        public int EnsureMaxInitialRouteBuildsPerTick;
        public float EnsureDistanceToFinalTarget;
        public float EnsureArrivalThreshold;
        public float EnsureRouteReuseDistanceThreshold;
        public string EnsureCurrentPlanetId;
        public string EnsureTargetSystemId;
        public string EnsureTargetPlanetId;
        public string EnsureTargetNpcId;
        public Vector3 EnsureEntryPosition;
        public Vector3 EnsureRawTargetPosition;
        public Vector3 EnsureFinalTargetPosition;

        public void Reset()
        {
            TargetResolveStats.Reset();

            EnsureBudgetEnterResetMs = 0d;
            EnsureBudgetEnterPlanetLaunchPopulationMs = 0d;
            EnsureBudgetEnterTotalBudgetMs = 0d;
            EnsureBudgetEnterRefreshBudgetMs = 0d;
            EnsureBudgetKind = string.Empty;
            EnsureBudgetEnterDecision = string.Empty;

            EnsureEntrySnapshotMs = 0d;
            EnsureArrivalMathMs = 0d;
            EnsureRouteReuseDistanceMs = 0d;
            EnsureBudgetCheckMs = 0d;
            EnsureBudgetEnterMs = 0d;

            EnsureFallbackMapConfigMs = 0d;
            EnsureFallbackMapPatrolBoundsMs = 0d;
            EnsureFallbackMapSetupMs = 0d;
            EnsureFallbackMapLoopMs = 0d;
            EnsureFallbackMapRandomPositionMs = 0d;
            EnsureFallbackMapInvalidPointCheckMs = 0d;
            EnsureFallbackMapAssignMs = 0d;

            EnsureFallbackMapUsedFallback = false;
            EnsureFallbackMapAttemptsConfigured = 0;
            EnsureFallbackMapAttemptsChecked = 0;

            EnsureFallbackRerollTotalMs = 0d;
            EnsureFallbackRerollDispatchMs = 0d;
            EnsureFallbackRerollClearRouteMs = 0d;
            EnsureFallbackRerollResetStateMs = 0d;
            EnsureFallbackRerollLogMs = 0d;

            EnsureFallbackSystemExitConfigMs = 0d;
            EnsureFallbackSystemExitRouteServiceMs = 0d;
            EnsureFallbackSystemExitSetupMs = 0d;
            EnsureFallbackSystemExitLoopMs = 0d;
            EnsureFallbackSystemExitInvalidPointCheckMs = 0d;
            EnsureFallbackSystemExitAssignMs = 0d;

            EnsureFallbackRerolled = false;
            EnsureFallbackSystemExitUsedFallback = false;
            EnsureFallbackRerollKind = string.Empty;
            EnsureFallbackSystemExitRouteCount = 0;
            EnsureFallbackSystemExitAttemptsChecked = 0;

            CompleteWrapperTotalMs = 0d;
            CompleteDelayCheckMs = 0d;
            CompleteDelayCurrentSystemCheckMs = 0d;
            CompleteDelayTravelStateCheckMs = 0d;
            CompleteDelayRouteTargetKindMs = 0d;
            CompleteDelayCounterMs = 0d;
            CompleteMovementBodyMs = 0d;

            CompleteDelayWasApplied = false;
            CompleteDelayDecision = string.Empty;
            CompleteDelayCompletionsUsedBefore = 0;
            CompleteDelayCompletionsUsedAfter = 0;
            CompleteDelayMaxCompletionsPerTick = 0;

            TickInitialDebugLogMs = 0d;
            TickTargetPositionInitMs = 0d;
            TickEnsureOuterMs = 0d;
            TickRouteLookupAndRegisterMs = 0d;
            TickStartTurnOuterMs = 0d;
            TickRouteMoveOuterMs = 0d;
            TickPublishOuterMs = 0d;
            TickArrivalOrCompleteOuterMs = 0d;

            EnsureUpdateTargetPrecheckMs = 0d;
            EnsureUpdateTargetStartTurnMs = 0d;
            EnsureUpdateTargetPathLengthMs = 0d;
            EnsureUpdateTargetArrivalThresholdMs = 0d;
            EnsureUpdateTargetDistanceMathMs = 0d;
            EnsureUpdateTargetPointOnPathMs = 0d;
            EnsureUpdateTargetAssignMs = 0d;

            EnsureFastRouteTotalMs = 0d;
            EnsureFastRouteLookupMs = 0d;
            EnsureFastRouteReusableCheckMs = 0d;
            EnsureFastRouteUpdateTargetMs = 0d;
            EnsureFastRouteArrivalMathMs = 0d;
            EnsureFastRouteLogMs = 0d;
            EnsureFastRoutePathPointCount = 0;

            TickPreRouteOrInitialMs = 0d;
            TickWaitingInitialRouteMs = 0d;

            CompletePrecheckMs = 0d;
            CompleteClearRouteMs = 0d;
            CompleteAssignPositionMs = 0d;
            CompleteStateSwitchMs = 0d;
            CompleteSystemTravelMs = 0d;
            CompleteLogMs = 0d;
            CompletePublishEventMs = 0d;
            CompleteBehaviorMs = 0d;
            CompleteTotalMs = 0d;

            CompleteWasBlocked = false;
            CompleteWasSystemTravel = false;
            CompleteRouteTargetKind = string.Empty;
            CompleteRouteTargetMoveKind = string.Empty;
            CompleteBehaviorBefore = string.Empty;
            CompleteTravelStateBefore = string.Empty;
            CompleteTravelStateAfter = string.Empty;
            CompleteSystemBefore = string.Empty;
            CompleteSystemAfter = string.Empty;
            CompleteTargetSystemId = string.Empty;
            CompleteTargetPlanetId = string.Empty;
            CompleteCurrentPlanetBefore = string.Empty;
            CompleteCurrentPlanetAfter = string.Empty;

            EnsureDirectionMs = 0d;
            RouteLookupMs = 0d;
            StartTurnMs = 0d;
            ArrivalThresholdMs = 0d;
            PathLengthMs = 0d;
            DistanceMathMs = 0d;
            PointOnPathMs = 0d;
            DirectionOnPathMs = 0d;
            ApplyStateMs = 0d;
            PublishMs = 0d;
            ArrivalCheckMs = 0d;
            CompleteMs = 0d;
            ClearRouteMs = 0d;

            EnsureResolveTargetMs = 0d;
            EnsureBoundaryMs = 0d;
            EnsureArrivalBeforeBuildMs = 0d;
            EnsureActiveRouteLookupMs = 0d;
            EnsureReuseChecksMs = 0d;
            EnsureInitialLimitMs = 0d;
            EnsureBuildRouteMs = 0d;
            EnsureCreateRouteStateMs = 0d;
            EnsureUpdateTargetMs = 0d;
            EnsureFallbackMs = 0d;

            RouteMissingCount = 0;
            ArrivedAfterEnsureDirectionCount = 0;
            StartTurnConsumedCount = 0;
            CompletedCount = 0;
            ClearedRouteCount = 0;
            PublishedPositionChangedCount = 0;
            PathPointCountTotal = 0;
            MaxPathPointCount = 0;

            EnsureDecision = string.Empty;
            EnsureEntryRouteTargetKind = string.Empty;
            EnsureEntryRouteTargetMoveKind = string.Empty;
            EnsureExitRouteTargetKind = string.Empty;
            EnsureExitRouteTargetMoveKind = string.Empty;
            EnsureEntryIsOnPlanet = false;
            EnsureEntryWaitingForInitialRouteBuild = false;
            EnsureEntryReleaseFromPlanetAfterInitialRouteBuild = false;
            EnsureEntryHasActiveRoute = false;
            EnsureEntryHasSameContextRoute = false;
            EnsureEntryRouteWasReusable = false;
            EnsureBoundaryAdjusted = false;
            EnsureBuiltRoute = false;
            EnsureUsedExistingRouteAfterBuildFail = false;
            EnsureEntryRoutePathCount = 0;
            EnsureBuiltRoutePathCount = 0;
            EnsureInitialRouteBuildsUsedBefore = 0;
            EnsureInitialRouteBuildsUsedAfter = 0;
            EnsureMaxInitialRouteBuildsPerTick = 0;
            EnsureDistanceToFinalTarget = 0f;
            EnsureArrivalThreshold = 0f;
            EnsureRouteReuseDistanceThreshold = 0f;
            EnsureCurrentPlanetId = string.Empty;
            EnsureTargetSystemId = string.Empty;
            EnsureTargetPlanetId = string.Empty;
            EnsureTargetNpcId = string.Empty;
            EnsureEntryPosition = Vector3.zero;
            EnsureRawTargetPosition = Vector3.zero;
            EnsureFinalTargetPosition = Vector3.zero;
        }

        public void Add(NpcDetailedMovementPhaseStats other)
        {
            if (other == null)
                return;

            EnsureBudgetEnterResetMs += other.EnsureBudgetEnterResetMs;
            EnsureBudgetEnterPlanetLaunchPopulationMs += other.EnsureBudgetEnterPlanetLaunchPopulationMs;
            EnsureBudgetEnterTotalBudgetMs += other.EnsureBudgetEnterTotalBudgetMs;
            EnsureBudgetEnterRefreshBudgetMs += other.EnsureBudgetEnterRefreshBudgetMs;

            if (!string.IsNullOrWhiteSpace(other.EnsureBudgetKind))
                EnsureBudgetKind = other.EnsureBudgetKind;

            if (!string.IsNullOrWhiteSpace(other.EnsureBudgetEnterDecision))
                EnsureBudgetEnterDecision = other.EnsureBudgetEnterDecision;

            EnsureEntrySnapshotMs += other.EnsureEntrySnapshotMs;
            EnsureArrivalMathMs += other.EnsureArrivalMathMs;
            EnsureRouteReuseDistanceMs += other.EnsureRouteReuseDistanceMs;
            EnsureBudgetCheckMs += other.EnsureBudgetCheckMs;
            EnsureBudgetEnterMs += other.EnsureBudgetEnterMs;

            EnsureFallbackMapConfigMs += other.EnsureFallbackMapConfigMs;
            EnsureFallbackMapPatrolBoundsMs += other.EnsureFallbackMapPatrolBoundsMs;
            EnsureFallbackMapSetupMs += other.EnsureFallbackMapSetupMs;
            EnsureFallbackMapLoopMs += other.EnsureFallbackMapLoopMs;
            EnsureFallbackMapRandomPositionMs += other.EnsureFallbackMapRandomPositionMs;
            EnsureFallbackMapInvalidPointCheckMs += other.EnsureFallbackMapInvalidPointCheckMs;
            EnsureFallbackMapAssignMs += other.EnsureFallbackMapAssignMs;

            EnsureFallbackMapUsedFallback |= other.EnsureFallbackMapUsedFallback;

            EnsureFallbackMapAttemptsConfigured = Math.Max(
                EnsureFallbackMapAttemptsConfigured,
                other.EnsureFallbackMapAttemptsConfigured);

            EnsureFallbackMapAttemptsChecked += other.EnsureFallbackMapAttemptsChecked;

            EnsureFallbackRerollTotalMs += other.EnsureFallbackRerollTotalMs;
            EnsureFallbackRerollDispatchMs += other.EnsureFallbackRerollDispatchMs;
            EnsureFallbackRerollClearRouteMs += other.EnsureFallbackRerollClearRouteMs;
            EnsureFallbackRerollResetStateMs += other.EnsureFallbackRerollResetStateMs;
            EnsureFallbackRerollLogMs += other.EnsureFallbackRerollLogMs;

            EnsureFallbackSystemExitConfigMs += other.EnsureFallbackSystemExitConfigMs;
            EnsureFallbackSystemExitRouteServiceMs += other.EnsureFallbackSystemExitRouteServiceMs;
            EnsureFallbackSystemExitSetupMs += other.EnsureFallbackSystemExitSetupMs;
            EnsureFallbackSystemExitLoopMs += other.EnsureFallbackSystemExitLoopMs;
            EnsureFallbackSystemExitInvalidPointCheckMs += other.EnsureFallbackSystemExitInvalidPointCheckMs;
            EnsureFallbackSystemExitAssignMs += other.EnsureFallbackSystemExitAssignMs;

            EnsureFallbackRerolled |= other.EnsureFallbackRerolled;
            EnsureFallbackSystemExitUsedFallback |= other.EnsureFallbackSystemExitUsedFallback;

            if (!string.IsNullOrWhiteSpace(other.EnsureFallbackRerollKind))
                EnsureFallbackRerollKind = other.EnsureFallbackRerollKind;

            EnsureFallbackSystemExitRouteCount = Math.Max(
                EnsureFallbackSystemExitRouteCount,
                other.EnsureFallbackSystemExitRouteCount);

            EnsureFallbackSystemExitAttemptsChecked += other.EnsureFallbackSystemExitAttemptsChecked;

            CompleteWrapperTotalMs += other.CompleteWrapperTotalMs;
            CompleteDelayCheckMs += other.CompleteDelayCheckMs;
            CompleteDelayCurrentSystemCheckMs += other.CompleteDelayCurrentSystemCheckMs;
            CompleteDelayTravelStateCheckMs += other.CompleteDelayTravelStateCheckMs;
            CompleteDelayRouteTargetKindMs += other.CompleteDelayRouteTargetKindMs;
            CompleteDelayCounterMs += other.CompleteDelayCounterMs;
            CompleteMovementBodyMs += other.CompleteMovementBodyMs;

            CompleteDelayWasApplied |= other.CompleteDelayWasApplied;

            if (!string.IsNullOrWhiteSpace(other.CompleteDelayDecision))
                CompleteDelayDecision = other.CompleteDelayDecision;

            CompleteDelayCompletionsUsedBefore = other.CompleteDelayCompletionsUsedBefore;
            CompleteDelayCompletionsUsedAfter = other.CompleteDelayCompletionsUsedAfter;
            CompleteDelayMaxCompletionsPerTick = other.CompleteDelayMaxCompletionsPerTick;

            TickInitialDebugLogMs += other.TickInitialDebugLogMs;
            TickTargetPositionInitMs += other.TickTargetPositionInitMs;
            TickEnsureOuterMs += other.TickEnsureOuterMs;
            TickRouteLookupAndRegisterMs += other.TickRouteLookupAndRegisterMs;
            TickStartTurnOuterMs += other.TickStartTurnOuterMs;
            TickRouteMoveOuterMs += other.TickRouteMoveOuterMs;
            TickPublishOuterMs += other.TickPublishOuterMs;
            TickArrivalOrCompleteOuterMs += other.TickArrivalOrCompleteOuterMs;

            EnsureUpdateTargetPrecheckMs += other.EnsureUpdateTargetPrecheckMs;
            EnsureUpdateTargetStartTurnMs += other.EnsureUpdateTargetStartTurnMs;
            EnsureUpdateTargetPathLengthMs += other.EnsureUpdateTargetPathLengthMs;
            EnsureUpdateTargetArrivalThresholdMs += other.EnsureUpdateTargetArrivalThresholdMs;
            EnsureUpdateTargetDistanceMathMs += other.EnsureUpdateTargetDistanceMathMs;
            EnsureUpdateTargetPointOnPathMs += other.EnsureUpdateTargetPointOnPathMs;
            EnsureUpdateTargetAssignMs += other.EnsureUpdateTargetAssignMs;

            EnsureFastRouteTotalMs += other.EnsureFastRouteTotalMs;
            EnsureFastRouteLookupMs += other.EnsureFastRouteLookupMs;
            EnsureFastRouteReusableCheckMs += other.EnsureFastRouteReusableCheckMs;
            EnsureFastRouteUpdateTargetMs += other.EnsureFastRouteUpdateTargetMs;
            EnsureFastRouteArrivalMathMs += other.EnsureFastRouteArrivalMathMs;
            EnsureFastRouteLogMs += other.EnsureFastRouteLogMs;

            if (other.EnsureFastRoutePathPointCount > EnsureFastRoutePathPointCount)
                EnsureFastRoutePathPointCount = other.EnsureFastRoutePathPointCount;

            TickPreRouteOrInitialMs += other.TickPreRouteOrInitialMs;
            TickWaitingInitialRouteMs += other.TickWaitingInitialRouteMs;

            TargetResolveStats.Add(other.TargetResolveStats);

            EnsureDirectionMs += other.EnsureDirectionMs;
            RouteLookupMs += other.RouteLookupMs;
            StartTurnMs += other.StartTurnMs;
            ArrivalThresholdMs += other.ArrivalThresholdMs;
            PathLengthMs += other.PathLengthMs;
            DistanceMathMs += other.DistanceMathMs;
            PointOnPathMs += other.PointOnPathMs;
            DirectionOnPathMs += other.DirectionOnPathMs;
            ApplyStateMs += other.ApplyStateMs;
            PublishMs += other.PublishMs;
            ArrivalCheckMs += other.ArrivalCheckMs;
            CompleteMs += other.CompleteMs;
            ClearRouteMs += other.ClearRouteMs;

            EnsureResolveTargetMs += other.EnsureResolveTargetMs;
            EnsureBoundaryMs += other.EnsureBoundaryMs;
            EnsureArrivalBeforeBuildMs += other.EnsureArrivalBeforeBuildMs;
            EnsureActiveRouteLookupMs += other.EnsureActiveRouteLookupMs;
            EnsureReuseChecksMs += other.EnsureReuseChecksMs;
            EnsureInitialLimitMs += other.EnsureInitialLimitMs;
            EnsureBuildRouteMs += other.EnsureBuildRouteMs;
            EnsureCreateRouteStateMs += other.EnsureCreateRouteStateMs;
            EnsureUpdateTargetMs += other.EnsureUpdateTargetMs;
            EnsureFallbackMs += other.EnsureFallbackMs;

            RouteMissingCount += other.RouteMissingCount;
            ArrivedAfterEnsureDirectionCount += other.ArrivedAfterEnsureDirectionCount;
            StartTurnConsumedCount += other.StartTurnConsumedCount;
            CompletedCount += other.CompletedCount;
            ClearedRouteCount += other.ClearedRouteCount;
            PublishedPositionChangedCount += other.PublishedPositionChangedCount;
            PathPointCountTotal += other.PathPointCountTotal;

            if (other.MaxPathPointCount > MaxPathPointCount)
                MaxPathPointCount = other.MaxPathPointCount;

            CompletePrecheckMs += other.CompletePrecheckMs;
            CompleteClearRouteMs += other.CompleteClearRouteMs;
            CompleteAssignPositionMs += other.CompleteAssignPositionMs;
            CompleteStateSwitchMs += other.CompleteStateSwitchMs;
            CompleteSystemTravelMs += other.CompleteSystemTravelMs;
            CompleteLogMs += other.CompleteLogMs;
            CompletePublishEventMs += other.CompletePublishEventMs;
            CompleteBehaviorMs += other.CompleteBehaviorMs;
            CompleteTotalMs += other.CompleteTotalMs;
        }

        public void CopyFrom(NpcDetailedMovementPhaseStats other)
        {
            Reset();

            if (other == null)
                return;

            Add(other);

            CompleteDelayWasApplied = other.CompleteDelayWasApplied;
            CompleteDelayDecision = other.CompleteDelayDecision;
            CompleteDelayCompletionsUsedBefore = other.CompleteDelayCompletionsUsedBefore;
            CompleteDelayCompletionsUsedAfter = other.CompleteDelayCompletionsUsedAfter;
            CompleteDelayMaxCompletionsPerTick = other.CompleteDelayMaxCompletionsPerTick;

            TargetResolveStats.CopyFrom(other.TargetResolveStats);

            CompleteWasBlocked = other.CompleteWasBlocked;
            CompleteWasSystemTravel = other.CompleteWasSystemTravel;
            CompleteRouteTargetKind = other.CompleteRouteTargetKind;
            CompleteRouteTargetMoveKind = other.CompleteRouteTargetMoveKind;
            CompleteBehaviorBefore = other.CompleteBehaviorBefore;
            CompleteTravelStateBefore = other.CompleteTravelStateBefore;
            CompleteTravelStateAfter = other.CompleteTravelStateAfter;
            CompleteSystemBefore = other.CompleteSystemBefore;
            CompleteSystemAfter = other.CompleteSystemAfter;
            CompleteTargetSystemId = other.CompleteTargetSystemId;
            CompleteTargetPlanetId = other.CompleteTargetPlanetId;
            CompleteCurrentPlanetBefore = other.CompleteCurrentPlanetBefore;
            CompleteCurrentPlanetAfter = other.CompleteCurrentPlanetAfter;

            EnsureDecision = other.EnsureDecision;
            EnsureEntryRouteTargetKind = other.EnsureEntryRouteTargetKind;
            EnsureEntryRouteTargetMoveKind = other.EnsureEntryRouteTargetMoveKind;
            EnsureExitRouteTargetKind = other.EnsureExitRouteTargetKind;
            EnsureExitRouteTargetMoveKind = other.EnsureExitRouteTargetMoveKind;
            EnsureEntryIsOnPlanet = other.EnsureEntryIsOnPlanet;
            EnsureEntryWaitingForInitialRouteBuild = other.EnsureEntryWaitingForInitialRouteBuild;
            EnsureEntryReleaseFromPlanetAfterInitialRouteBuild = other.EnsureEntryReleaseFromPlanetAfterInitialRouteBuild;
            EnsureEntryHasActiveRoute = other.EnsureEntryHasActiveRoute;
            EnsureEntryHasSameContextRoute = other.EnsureEntryHasSameContextRoute;
            EnsureEntryRouteWasReusable = other.EnsureEntryRouteWasReusable;
            EnsureBoundaryAdjusted = other.EnsureBoundaryAdjusted;
            EnsureBuiltRoute = other.EnsureBuiltRoute;
            EnsureUsedExistingRouteAfterBuildFail = other.EnsureUsedExistingRouteAfterBuildFail;
            EnsureEntryRoutePathCount = other.EnsureEntryRoutePathCount;
            EnsureBuiltRoutePathCount = other.EnsureBuiltRoutePathCount;
            EnsureInitialRouteBuildsUsedBefore = other.EnsureInitialRouteBuildsUsedBefore;
            EnsureInitialRouteBuildsUsedAfter = other.EnsureInitialRouteBuildsUsedAfter;
            EnsureMaxInitialRouteBuildsPerTick = other.EnsureMaxInitialRouteBuildsPerTick;
            EnsureDistanceToFinalTarget = other.EnsureDistanceToFinalTarget;
            EnsureArrivalThreshold = other.EnsureArrivalThreshold;
            EnsureRouteReuseDistanceThreshold = other.EnsureRouteReuseDistanceThreshold;
            EnsureCurrentPlanetId = other.EnsureCurrentPlanetId;
            EnsureTargetSystemId = other.EnsureTargetSystemId;
            EnsureTargetPlanetId = other.EnsureTargetPlanetId;
            EnsureTargetNpcId = other.EnsureTargetNpcId;
            EnsureEntryPosition = other.EnsureEntryPosition;
            EnsureRawTargetPosition = other.EnsureRawTargetPosition;
            EnsureFinalTargetPosition = other.EnsureFinalTargetPosition;
        }

        public void RegisterPath(IReadOnlyList<Vector3> path)
        {
            int pathPointCount =
                path != null
                    ? path.Count
                    : 0;

            PathPointCountTotal += pathPointCount;

            if (pathPointCount > MaxPathPointCount)
                MaxPathPointCount = pathPointCount;
        }

        public string GetDominantPhaseName()
        {
            double maxMs = EnsureDirectionMs;
            string phaseName = "EnsureDirection";

            SetDominantPhase(ref maxMs, ref phaseName, "RouteLookup", RouteLookupMs);
            SetDominantPhase(ref maxMs, ref phaseName, "StartTurn", StartTurnMs);
            SetDominantPhase(ref maxMs, ref phaseName, "ArrivalThreshold", ArrivalThresholdMs);
            SetDominantPhase(ref maxMs, ref phaseName, "PathLength", PathLengthMs);
            SetDominantPhase(ref maxMs, ref phaseName, "DistanceMath", DistanceMathMs);
            SetDominantPhase(ref maxMs, ref phaseName, "PointOnPath", PointOnPathMs);
            SetDominantPhase(ref maxMs, ref phaseName, "DirectionOnPath", DirectionOnPathMs);
            SetDominantPhase(ref maxMs, ref phaseName, "ApplyState", ApplyStateMs);
            SetDominantPhase(ref maxMs, ref phaseName, "Publish", PublishMs);
            SetDominantPhase(ref maxMs, ref phaseName, "ArrivalCheck", ArrivalCheckMs);
            SetDominantPhase(ref maxMs, ref phaseName, "Complete", CompleteMs);
            SetDominantPhase(ref maxMs, ref phaseName, "ClearRoute", ClearRouteMs);

            return phaseName;
        }

        public double GetDominantPhaseMs()
        {
            double maxMs = EnsureDirectionMs;

            maxMs = Math.Max(maxMs, RouteLookupMs);
            maxMs = Math.Max(maxMs, StartTurnMs);
            maxMs = Math.Max(maxMs, ArrivalThresholdMs);
            maxMs = Math.Max(maxMs, PathLengthMs);
            maxMs = Math.Max(maxMs, DistanceMathMs);
            maxMs = Math.Max(maxMs, PointOnPathMs);
            maxMs = Math.Max(maxMs, DirectionOnPathMs);
            maxMs = Math.Max(maxMs, ApplyStateMs);
            maxMs = Math.Max(maxMs, PublishMs);
            maxMs = Math.Max(maxMs, ArrivalCheckMs);
            maxMs = Math.Max(maxMs, CompleteMs);
            maxMs = Math.Max(maxMs, ClearRouteMs);

            return maxMs;
        }

        private static void SetDominantPhase(
            ref double maxMs,
            ref string phaseName,
            string candidatePhaseName,
            double candidateMs)
        {
            if (candidateMs <= maxMs)
                return;

            maxMs = candidateMs;
            phaseName = candidatePhaseName;
        }
    }

    private int _currentSystemShipsInSpaceBudgetTick = -1;
    private string _currentSystemShipsInSpaceBudgetSystemId = string.Empty;
    private int _currentSystemShipsInSpaceBudgetCount;
    private bool _isInitialWarmupRouteBuild;
    private const float ArrivalDistanceThreshold = 3f;
    private const float SunAvoidanceSafetyMargin = 80f;
    private const int SunAvoidanceArcSegments = 18;
    private const float SunAvoidanceDestinationRefreshThreshold = 40f;
    private const float DirectionThresholdSqrMagnitude = 0.0001f;
    private const double PerfLogThresholdMs = 2.0;
    private const int DefaultMaxInitialRouteBuildsPerTick = 20;
    private int _initialRouteBuildTick = int.MinValue;
    private int _initialRouteBuildsUsedThisTick;
    private int _routeBuildBudgetTick = int.MinValue;
    private double _routeBuildBudgetUsedMs;
    private double _routeRefreshBudgetUsedMs;
    private const int DefaultMaxSystemExitCompletionsPerTick = 2;
    private int _systemExitCompletionTick = int.MinValue;
    private int _systemExitCompletionsUsedThisTick;

    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly ISystemNpcBehaviorService _behaviorService;
    private readonly ISystemNpcMovementRouteService _routeService;
    private readonly IConfigService _configService;
    private IGameTimeService _gameTimeService;
    private readonly SimpleEventBus _eventBus;
    private readonly IGameSessionService _gameSessionService;
    private readonly ISystemShipRouteService2A _shipRouteService;
    private readonly ISystemShipRoutePlanner2A _sharedRoutePlanner;
    private readonly SystemSharedShipRoutePlanResult2A _sharedRoutePlanResult =
        new SystemSharedShipRoutePlanResult2A();
    private readonly SystemShipRouteResult2A _npcRouteBuildResult =
        new SystemShipRouteResult2A();

    private readonly List<Vector3> _sunAvoidancePath = new();
    private readonly Dictionary<string, SunAvoidanceRouteState> _sunAvoidanceRoutes = new();

    private const int NpcRoutePlanMaxSteps = 8192;
    private readonly Dictionary<string, NpcMovementRouteState> _npcMovementRoutes = new();
    private readonly Dictionary<string, NpcRoutePerfSnapshot> _npcRoutePerfSnapshots = new();
    private readonly Dictionary<string, SystemBoundaryNavigation2A.BoundaryNavigationState> _npcBoundaryNavigationStates = new();
    private readonly List<SystemNpcRuntimeState> _npcMovementPriorityBuffer = new();
    private const int DefaultPriorityFinalApproachRouteBuildsPerTick = 4;
    private readonly List<float> _npcMovementPriorityDistanceBuffer = new();
    private readonly List<SystemNpcRuntimeState> _npcMovementRegularBuffer = new();
    private readonly HashSet<SystemNpcRuntimeState> _npcMovementPrioritySet = new();
    private readonly NpcBehaviorActionAnalytics _behaviorActionAnalytics = new NpcBehaviorActionAnalytics();
    private readonly List<Vector3> _npcRouteWaypointsBuffer = new();
    private readonly List<Vector3> _npcRoutePreviewPathBuffer = new();
    private readonly List<Vector3> _npcRouteTailPathBuffer = new();
    private readonly List<Vector3> _npcRouteSegmentPathBuffer = new();

    public SystemNpcMovementService()
    {
        _debugEnabled = false;
        _debugStop = true;

        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _behaviorService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementRouteService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _shipRouteService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemShipRouteService2A>();
        _sharedRoutePlanner = Bootstrapper.Instance.ServiceRegistry.Get<ISystemShipRoutePlanner2A>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        LogCustom(
            "[NpcRouteDebug] Service created | " +
            "RuntimeService = " + (_runtimeService != null) +
            " | BehaviorService = " + (_behaviorService != null) +
            " | RouteService = " + (_routeService != null) +
            " | ConfigService = " + (_configService != null) +
            " | ShipRouteService = " + (_shipRouteService != null) +
            " | SharedRoutePlanner = " + (_sharedRoutePlanner != null) +
            " | EventBus = " + (_eventBus != null));
    }

    public void Tick(StarSystemConfig starSystem, float deltaTime, int currentTick)
    {
        long totalStartedAt = BeginPerfMeasure();

        if (starSystem == null || string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        if (deltaTime <= 0f)
            return;

        long tickSetupStartedAt = BeginPerfMeasure();

        _initialRouteBuildTick = currentTick;
        _initialRouteBuildsUsedThisTick = 0;

        double tickSetupMs = EndPerfMeasureMs(tickSetupStartedAt);

        long resetRouteBuildBudgetStartedAt = BeginPerfMeasure();

        ResetRouteBuildBudgetForTick(currentTick);

        double resetRouteBuildBudgetMs = EndPerfMeasureMs(resetRouteBuildBudgetStartedAt);

        long behaviorAnalyticsResetStartedAt = BeginPerfMeasure();

        _behaviorActionAnalytics.Reset();

        double behaviorAnalyticsResetMs = EndPerfMeasureMs(behaviorAnalyticsResetStartedAt);

        long scopeSetupStartedAt = BeginPerfMeasure();

        string currentSystemId = GetCurrentSystemIdForPerf();
        string scope = GetPerfScope(starSystem.Id);
        bool loadAnalyticsEnabled = IsNpcMovementLoadAnalyticsEnabled();

        bool currentSystemDetailEnabled =
            string.Equals(scope, "CURRENT_SYSTEM", StringComparison.Ordinal) &&
            Bootstrapper.Instance != null &&
            Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics);

        bool collectDetailedMovementAnalytics =
            loadAnalyticsEnabled || currentSystemDetailEnabled;

        double scopeSetupMs = EndPerfMeasureMs(scopeSetupStartedAt);

        long statsAllocationStartedAt = BeginPerfMeasure();

        NpcDetailedMovementPhaseStats aggregatePhaseStats =
            collectDetailedMovementAnalytics ? new NpcDetailedMovementPhaseStats() : null;

        NpcDetailedMovementPhaseStats npcPhaseStats =
            collectDetailedMovementAnalytics ? new NpcDetailedMovementPhaseStats() : null;

        NpcDetailedMovementPhaseStats maxFixedSystemExitPointPhaseStats =
            collectDetailedMovementAnalytics ? new NpcDetailedMovementPhaseStats() : null;

        double statsAllocationMs = EndPerfMeasureMs(statsAllocationStartedAt);

        long getAliveNpcsStartedAt = BeginPerfMeasure();

        IReadOnlyList<SystemNpcRuntimeState> aliveNpcs =
            _runtimeService.GetAliveNpcsInSystem(starSystem.Id);

        double getAliveNpcsMs = EndPerfMeasureMs(getAliveNpcsStartedAt);

        long prioritizeNpcsStartedAt = BeginPerfMeasure();

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            PrioritizeNpcMovementOrder(aliveNpcs);

        double prioritizeNpcsMs = EndPerfMeasureMs(prioritizeNpcsStartedAt);
        double getNpcsMs = getAliveNpcsMs + prioritizeNpcsMs;

        int npcCount = npcs != null ? npcs.Count : 0;
        PrimeCurrentSystemShipsInSpaceBudgetCache(
            starSystem.Id,
            npcs,
            currentTick);
        int movedCount = 0;
        int blockedCount = 0;

        int blockedNullCount = 0;
        int blockedNotAliveCount = 0;
        int blockedIsOnPlanetCount = 0;
        int blockedTravelStateIdleCount = 0;
        int blockedTravelStateOnPlanetCount = 0;
        int blockedUnknownCount = 0;
        int blockedInSpaceCount = 0;

        int fixedPatrolPointCount = 0;
        int fixedSystemExitPointCount = 0;
        int movingPlanetCount = 0;
        int movingNpcOrEnemyCount = 0;
        int fixedMapPointCount = 0;
        int unknownRouteTargetCount = 0;

        double fixedPatrolPointMs = 0d;
        double fixedSystemExitPointMs = 0d;
        double movingPlanetMs = 0d;
        double movingNpcOrEnemyMs = 0d;
        double fixedMapPointMs = 0d;
        double unknownRouteTargetMs = 0d;

        double maxNpcMs = 0d;
        string maxNpcId = string.Empty;
        string maxNpcRouteTargetKind = string.Empty;
        string maxNpcRouteTargetMoveKind = string.Empty;

        double maxFixedSystemExitPointNpcMs = 0d;
        string maxFixedSystemExitPointNpcId = string.Empty;
        string maxFixedSystemExitPointBehavior = string.Empty;
        string maxFixedSystemExitPointTravelState = string.Empty;
        string maxFixedSystemExitPointTargetSystemId = string.Empty;
        string maxFixedSystemExitPointTargetPlanetId = string.Empty;
        string maxFixedSystemExitPointRouteTargetKindAfter = string.Empty;
        string maxFixedSystemExitPointRouteTargetMoveKindAfter = string.Empty;

        long loopStartedAt = BeginPerfMeasure();

        for (int i = 0; i < npcCount; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc == null)
            {
                blockedCount++;
                blockedNullCount++;
                continue;
            }

            SystemNpcBehaviorType behaviorBefore = npc.CurrentBehavior;
            SystemNpcTravelState travelStateBefore = npc.TravelState;

            _behaviorActionAnalytics.AddNpc(behaviorBefore);

            string routeTargetKindBefore =
                collectDetailedMovementAnalytics
                    ? GetNpcRouteTargetKind(npc)
                    : string.Empty;

            string routeTargetMoveKindBefore =
                collectDetailedMovementAnalytics
                    ? GetNpcRouteTargetMoveKind(npc)
                    : string.Empty;

            if (collectDetailedMovementAnalytics)
            {
                if (routeTargetKindBefore == "FIXED_PATROL_POINT")
                    fixedPatrolPointCount++;
                else if (routeTargetKindBefore == "FIXED_SYSTEM_EXIT_POINT")
                    fixedSystemExitPointCount++;
                else if (routeTargetKindBefore == "MOVING_PLANET")
                    movingPlanetCount++;
                else if (routeTargetKindBefore == "MOVING_NPC_OR_ENEMY")
                    movingNpcOrEnemyCount++;
                else if (routeTargetKindBefore == "FIXED_MAP_POINT")
                    fixedMapPointCount++;
                else
                    unknownRouteTargetCount++;
            }

            if (!CanMove(npc))
            {
                blockedCount++;

                string blockReason = GetMovementBlockReason(npc);

                if (blockReason == "NotAlive")
                    blockedNotAliveCount++;
                else if (blockReason == "IsOnPlanet")
                    blockedIsOnPlanetCount++;
                else if (blockReason == "TravelStateIdle")
                    blockedTravelStateIdleCount++;
                else if (blockReason == "TravelStateOnPlanet")
                    blockedTravelStateOnPlanetCount++;
                else
                    blockedUnknownCount++;

                if (!npc.IsOnPlanet)
                    blockedInSpaceCount++;

                continue;
            }

            if (npcPhaseStats != null)
                npcPhaseStats.Reset();

            long npcStartedAt = BeginPerfMeasure();

            TickNpcMovement(
                npc,
                deltaTime,
                currentTick,
                npcPhaseStats);

            double npcMs = EndPerfMeasureMs(npcStartedAt);

            AddNpcBehaviorActionAnalytics(
                behaviorBefore,
                npcPhaseStats);

            if (aggregatePhaseStats != null && npcPhaseStats != null)
                aggregatePhaseStats.Add(npcPhaseStats);

            if (collectDetailedMovementAnalytics)
            {
                if (routeTargetKindBefore == "FIXED_PATROL_POINT")
                    fixedPatrolPointMs += npcMs;
                else if (routeTargetKindBefore == "FIXED_SYSTEM_EXIT_POINT")
                    fixedSystemExitPointMs += npcMs;
                else if (routeTargetKindBefore == "MOVING_PLANET")
                    movingPlanetMs += npcMs;
                else if (routeTargetKindBefore == "MOVING_NPC_OR_ENEMY")
                    movingNpcOrEnemyMs += npcMs;
                else if (routeTargetKindBefore == "FIXED_MAP_POINT")
                    fixedMapPointMs += npcMs;
                else
                    unknownRouteTargetMs += npcMs;

                if (routeTargetKindBefore == "FIXED_SYSTEM_EXIT_POINT" &&
                    npcMs > maxFixedSystemExitPointNpcMs)
                {
                    maxFixedSystemExitPointNpcMs = npcMs;
                    maxFixedSystemExitPointNpcId = npc.RuntimeNpcId;
                    maxFixedSystemExitPointBehavior = npc.CurrentBehavior.ToString();
                    maxFixedSystemExitPointTravelState = npc.TravelState.ToString();
                    maxFixedSystemExitPointTargetSystemId = npc.TargetSystemId ?? string.Empty;
                    maxFixedSystemExitPointTargetPlanetId = npc.TargetPlanetId ?? string.Empty;
                    maxFixedSystemExitPointRouteTargetKindAfter = GetNpcRouteTargetKind(npc);
                    maxFixedSystemExitPointRouteTargetMoveKindAfter = GetNpcRouteTargetMoveKind(npc);

                    if (maxFixedSystemExitPointPhaseStats != null)
                        maxFixedSystemExitPointPhaseStats.CopyFrom(npcPhaseStats);
                }
            }

            if (npcMs > maxNpcMs)
            {
                maxNpcMs = npcMs;
                maxNpcId = npc.RuntimeNpcId;
                maxNpcRouteTargetKind = GetNpcRouteTargetKind(npc);
                maxNpcRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            if (ShouldLogNpcPerf(npcMs))
            {
                LogNpcPerf(
                    npcMs,
                    "SystemNpcMovementService.TickNpcMovement" +
                    " | Scope=" + scope +
                    " | CurrentSystemId=" + currentSystemId +
                    " | TickSystemId=" + starSystem.Id +
                    " | Tick=" + currentTick +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Type=" + npc.NpcType +
                    " | BehaviorBefore=" + behaviorBefore +
                    " | BehaviorAfter=" + npc.CurrentBehavior +
                    " | TravelStateBefore=" + travelStateBefore +
                    " | TravelStateAfter=" + npc.TravelState +
                    " | RouteTargetKindBefore=" + routeTargetKindBefore +
                    " | RouteTargetMoveKindBefore=" + routeTargetMoveKindBefore +
                    GetNpcRouteTargetLogFields(npc) +
                    BuildNpcTickMovementPhaseLogFields(npcPhaseStats, npcMs) +
                    " | Speed=" + npc.Speed.ToString("0.###"));
            }

            movedCount++;
        }

        double loopMs = EndPerfMeasureMs(loopStartedAt);
        double totalMs = EndPerfMeasureMs(totalStartedAt);

        double tickPreLoopTrackedMs =
            tickSetupMs +
            resetRouteBuildBudgetMs +
            behaviorAnalyticsResetMs +
            scopeSetupMs +
            statsAllocationMs +
            getNpcsMs;

        double tickTopLevelTrackedMs =
            tickPreLoopTrackedMs +
            loopMs;

        double tickTopLevelUntrackedMs =
            Math.Max(0d, totalMs - tickTopLevelTrackedMs);

        bool shouldLogTickPerf = ShouldLogNpcPerf(totalMs);

        string dominantRouteTargetKind = string.Empty;
        double dominantRouteTargetMs = 0d;

        if (shouldLogTickPerf || collectDetailedMovementAnalytics)
        {
            dominantRouteTargetKind =
                GetDominantRouteTargetKindForPerf(
                    fixedPatrolPointMs,
                    fixedSystemExitPointMs,
                    movingPlanetMs,
                    movingNpcOrEnemyMs,
                    fixedMapPointMs,
                    unknownRouteTargetMs);

            dominantRouteTargetMs =
                GetDominantRouteTargetMsForPerf(
                    fixedPatrolPointMs,
                    fixedSystemExitPointMs,
                    movingPlanetMs,
                    movingNpcOrEnemyMs,
                    fixedMapPointMs,
                    unknownRouteTargetMs);
        }

        if (shouldLogTickPerf)
        {
            LogNpcPerf(
                totalMs,
                "SystemNpcMovementService.Tick" +
                " | SimulationPhase=" + GetNpcSimulationPhaseForAnalytics() +
                " | Scope=" + scope +
                " | CurrentSystemId=" + currentSystemId +
                " | TickSystemId=" + starSystem.Id +
                " | Tick=" + currentTick +
                " | DeltaTime=" + deltaTime.ToString("0.####") +
                " | Npcs=" + npcCount +
                " | Moved=" + movedCount +
                " | Blocked=" + blockedCount +
                " | TickSetupMs=" + tickSetupMs.ToString("F2") +
                " | ResetRouteBuildBudgetMs=" + resetRouteBuildBudgetMs.ToString("F2") +
                " | BehaviorAnalyticsResetMs=" + behaviorAnalyticsResetMs.ToString("F2") +
                " | ScopeSetupMs=" + scopeSetupMs.ToString("F2") +
                " | StatsAllocationMs=" + statsAllocationMs.ToString("F2") +
                " | GetNpcsMs=" + getNpcsMs.ToString("F2") +
                " | GetAliveNpcsMs=" + getAliveNpcsMs.ToString("F2") +
                " | PrioritizeNpcsMs=" + prioritizeNpcsMs.ToString("F2") +
                " | LoopMs=" + loopMs.ToString("F2") +
                " | TickPreLoopTrackedMs=" + tickPreLoopTrackedMs.ToString("F2") +
                " | TickTopLevelTrackedMs=" + tickTopLevelTrackedMs.ToString("F2") +
                " | TickTopLevelUntrackedMs=" + tickTopLevelUntrackedMs.ToString("F2") +
                " | MaxNpcMs=" + maxNpcMs.ToString("F2") +
                " | MaxNpc=" + maxNpcId +
                " | MaxNpcRouteTargetKind=" + maxNpcRouteTargetKind +
                " | MaxNpcRouteTargetMoveKind=" + maxNpcRouteTargetMoveKind +
                " | DominantRouteTargetKind=" + dominantRouteTargetKind +
                " | DominantRouteTargetMs=" + dominantRouteTargetMs.ToString("F2") +
                " | FixedSystemExitPointMs=" + fixedSystemExitPointMs.ToString("F2") +
                " | MaxFixedSystemExitPointNpcMs=" + maxFixedSystemExitPointNpcMs.ToString("F2") +
                " | MaxFixedSystemExitPointNpc=" + maxFixedSystemExitPointNpcId);
        }

        LogNpcBehaviorActionAnalytics(
            currentTick,
            deltaTime,
            totalMs,
            scope,
            currentSystemId,
            starSystem.Id,
            npcCount,
            movedCount,
            blockedCount,
            blockedNullCount,
            blockedNotAliveCount,
            blockedIsOnPlanetCount,
            blockedTravelStateIdleCount,
            blockedTravelStateOnPlanetCount,
            blockedUnknownCount,
            blockedInSpaceCount);

        if (loadAnalyticsEnabled)
        {
            int galaxyNpcsTotal = GetGalaxyNpcTotalCountForPerf();
            int galaxyNpcsAlive = GetGalaxyAliveNpcCountForPerf();
            int currentSystemNpcsAlive = CountAliveNpcsInSystemForPerf(currentSystemId);

            LogNpcMovementLoadAnalyticsIfNeeded(
                currentTick,
                deltaTime,
                totalMs,
                getNpcsMs,
                loopMs,
                scope,
                currentSystemId,
                starSystem.Id,
                galaxyNpcsTotal,
                galaxyNpcsAlive,
                currentSystemNpcsAlive,
                npcCount,
                movedCount,
                blockedCount,
                fixedPatrolPointCount,
                fixedSystemExitPointCount,
                movingPlanetCount,
                movingNpcOrEnemyCount,
                fixedMapPointCount,
                unknownRouteTargetCount,
                fixedPatrolPointMs,
                fixedSystemExitPointMs,
                movingPlanetMs,
                movingNpcOrEnemyMs,
                fixedMapPointMs,
                unknownRouteTargetMs,
                dominantRouteTargetKind,
                dominantRouteTargetMs,
                maxNpcMs,
                maxNpcId,
                maxNpcRouteTargetKind,
                maxNpcRouteTargetMoveKind,
                maxFixedSystemExitPointNpcMs,
                maxFixedSystemExitPointNpcId,
                maxFixedSystemExitPointBehavior,
                maxFixedSystemExitPointTravelState,
                maxFixedSystemExitPointTargetSystemId,
                maxFixedSystemExitPointTargetPlanetId,
                maxFixedSystemExitPointRouteTargetKindAfter,
                maxFixedSystemExitPointRouteTargetMoveKindAfter,
                aggregatePhaseStats,
                maxFixedSystemExitPointPhaseStats);
        }

        LogCurrentSystemNpcMovementDetailIfNeeded(
            currentTick,
            deltaTime,
            totalMs,
            getNpcsMs,
            getAliveNpcsMs,
            prioritizeNpcsMs,
            loopMs,
            scope,
            currentSystemId,
            starSystem.Id,
            npcCount,
            movedCount,
            blockedCount,
            fixedPatrolPointCount,
            fixedSystemExitPointCount,
            movingPlanetCount,
            movingNpcOrEnemyCount,
            fixedMapPointCount,
            unknownRouteTargetCount,
            fixedPatrolPointMs,
            fixedSystemExitPointMs,
            movingPlanetMs,
            movingNpcOrEnemyMs,
            fixedMapPointMs,
            unknownRouteTargetMs,
            dominantRouteTargetKind,
            dominantRouteTargetMs,
            maxNpcMs,
            maxNpcId,
            maxNpcRouteTargetKind,
            maxNpcRouteTargetMoveKind,
            aggregatePhaseStats);
    }

    private string GetDominantRouteTargetKindForPerf(
    double fixedPatrolPointMs,
    double fixedSystemExitPointMs,
    double movingPlanetMs,
    double movingNpcOrEnemyMs,
    double fixedMapPointMs,
    double unknownRouteTargetMs)
    {
        string dominantKind = "FIXED_PATROL_POINT";
        double dominantMs = fixedPatrolPointMs;

        SetDominantRouteTargetForPerf(
            ref dominantKind,
            ref dominantMs,
            "FIXED_SYSTEM_EXIT_POINT",
            fixedSystemExitPointMs);

        SetDominantRouteTargetForPerf(
            ref dominantKind,
            ref dominantMs,
            "MOVING_PLANET",
            movingPlanetMs);

        SetDominantRouteTargetForPerf(
            ref dominantKind,
            ref dominantMs,
            "MOVING_NPC_OR_ENEMY",
            movingNpcOrEnemyMs);

        SetDominantRouteTargetForPerf(
            ref dominantKind,
            ref dominantMs,
            "FIXED_MAP_POINT",
            fixedMapPointMs);

        SetDominantRouteTargetForPerf(
            ref dominantKind,
            ref dominantMs,
            "UNKNOWN_OR_FALLBACK",
            unknownRouteTargetMs);

        return dominantMs > 0d ? dominantKind : string.Empty;
    }

    private double GetDominantRouteTargetMsForPerf(
    double fixedPatrolPointMs,
    double fixedSystemExitPointMs,
    double movingPlanetMs,
    double movingNpcOrEnemyMs,
    double fixedMapPointMs,
    double unknownRouteTargetMs)
    {
        double dominantMs = fixedPatrolPointMs;

        dominantMs = Math.Max(dominantMs, fixedSystemExitPointMs);
        dominantMs = Math.Max(dominantMs, movingPlanetMs);
        dominantMs = Math.Max(dominantMs, movingNpcOrEnemyMs);
        dominantMs = Math.Max(dominantMs, fixedMapPointMs);
        dominantMs = Math.Max(dominantMs, unknownRouteTargetMs);

        return dominantMs;
    }

    private void SetDominantRouteTargetForPerf(
        ref string dominantKind,
        ref double dominantMs,
        string candidateKind,
        double candidateMs)
    {
        if (candidateMs <= dominantMs)
            return;

        dominantKind = candidateKind;
        dominantMs = candidateMs;
    }

    private string GetMovementBlockReason(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "NpcNull";

        if (!npc.IsAlive)
            return "NotAlive";

        if (npc.IsOnPlanet)
            return "IsOnPlanet";

        if (npc.TravelState == SystemNpcTravelState.Idle)
            return "TravelStateIdle";

        if (npc.TravelState == SystemNpcTravelState.OnPlanet)
            return "TravelStateOnPlanet";

        return "Unknown";
    }

    private bool CanMove(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!npc.IsAlive)
            return false;

        if (npc.IsWaitingForInitialRouteBuild)
            return true;

        if (npc.IsOnPlanet)
            return false;

        if (npc.TravelState == SystemNpcTravelState.Idle)
            return false;

        if (npc.TravelState == SystemNpcTravelState.OnPlanet)
            return false;

        return true;
    }

    private void TickNpcMovement(
        SystemNpcRuntimeState npc,
        float deltaTime,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long outerStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (IsNpcMilitaryMovementVerboseDebugEnabled() &&
                IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] Tick start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", Behavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", Position=" + npc.CurrentPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", CurrentMovementTargetPosition=" + npc.CurrentMovementTargetPosition +
                ", TickMovementTargetPosition=" + npc.TickMovementTargetPosition +
                ", TickMovementDirectionTick=" + npc.TickMovementDirectionTick +
                ", TickMovementArrived=" + npc.TickMovementArrived +
                ", WaitingForInitialRouteBuild=" + npc.IsWaitingForInitialRouteBuild +
                ", Speed=" + npc.Speed +
                ", DeltaTime=" + deltaTime +
                ", TickScaledDeltaTime=" + GetTickScaledDeltaTime(deltaTime) +
                ", SecondsPerTick=" + GameTimeState.SecondsPerDay +
                ", Tick=" + currentTick);
        }

        if (phaseStats != null)
            phaseStats.TickInitialDebugLogMs += EndPerfMeasureMs(outerStartedAt);

        if (npc.IsWaitingForInitialRouteBuild)
        {
            long waitingStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;
            double ensureBefore = phaseStats != null ? phaseStats.EnsureDirectionMs : 0d;

            TryBuildInitialRouteOrWait(
                npc,
                currentTick,
                phaseStats);

            if (phaseStats != null)
            {
                double waitingTotalMs =
                    EndPerfMeasureMs(waitingStartedAt);

                double ensureDeltaMs =
                    Math.Max(
                        0d,
                        phaseStats.EnsureDirectionMs - ensureBefore);

                phaseStats.TickPreRouteOrInitialMs += waitingTotalMs;
                phaseStats.TickWaitingInitialRouteMs += Math.Max(0d, waitingTotalMs - ensureDeltaMs);
                phaseStats.TickEnsureOuterMs += waitingTotalMs;
            }

            return;
        }

        outerStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc.TargetPosition == Vector3.zero)
        {
            npc.StartPosition = npc.CurrentPosition;
            npc.TravelProgress01 = 0f;
        }

        if (phaseStats != null)
            phaseStats.TickTargetPositionInitMs += EndPerfMeasureMs(outerStartedAt);

        long phaseStartedAt;

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;
        EnsureTickMovementDirection(npc, currentTick, phaseStats);

        if (phaseStats != null)
        {
            double ensureDirectionMs =
                EndPerfMeasureMs(phaseStartedAt);

            phaseStats.EnsureDirectionMs += ensureDirectionMs;
            phaseStats.TickPreRouteOrInitialMs += ensureDirectionMs;
            phaseStats.TickEnsureOuterMs += ensureDirectionMs;
        }

        if (npc.TickMovementArrived)
        {
            if (phaseStats != null)
                phaseStats.ArrivedAfterEnsureDirectionCount++;

            return;
        }

        outerStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        bool hasRoute =
            _npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) &&
            routeState != null &&
            routeState.Path != null &&
            routeState.Path.Count > 1 &&
            IsSameNpcMovementRouteContext(routeState, npc);

        if (phaseStats != null)
            phaseStats.RouteLookupMs += EndPerfMeasureMs(phaseStartedAt);

        if (!hasRoute)
        {
            if (phaseStats != null)
            {
                phaseStats.RouteMissingCount++;
                phaseStats.TickRouteLookupAndRegisterMs += EndPerfMeasureMs(outerStartedAt);
            }

            LogNpcPlanetApproachQueueCoordinates(
                "ROUTE_MISSING_AFTER_ENSURE",
                npc,
                currentTick,
                routeState);

            return;
        }

        if (phaseStats != null)
        {
            phaseStats.RegisterPath(routeState.Path);
            phaseStats.TickRouteLookupAndRegisterMs += EndPerfMeasureMs(outerStartedAt);
        }

        outerStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        bool consumedStartTurn =
            ConsumeNpcStartTurnInPlaceIfNeeded(
                npc,
                routeState,
                currentTick);

        if (phaseStats != null)
        {
            phaseStats.StartTurnMs += EndPerfMeasureMs(phaseStartedAt);
            phaseStats.TickStartTurnOuterMs += EndPerfMeasureMs(outerStartedAt);
        }

        if (consumedStartTurn)
        {
            if (phaseStats != null)
                phaseStats.StartTurnConsumedCount++;

            return;
        }

        outerStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                npc.TargetPosition);

        if (phaseStats != null)
            phaseStats.ArrivalThresholdMs += EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        if (phaseStats != null)
            phaseStats.PathLengthMs += EndPerfMeasureMs(phaseStartedAt);

        if (totalRouteLength <= arrivalThreshold)
        {
            phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            bool completed =
                TryCompleteMovementOrDelaySystemExit(
                    npc,
                    currentTick,
                    phaseStats);

            if (phaseStats != null)
            {
                phaseStats.CompleteMs += EndPerfMeasureMs(phaseStartedAt);
                phaseStats.TickArrivalOrCompleteOuterMs += EndPerfMeasureMs(outerStartedAt);

                if (completed)
                    phaseStats.CompletedCount++;
            }

            return;
        }

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        routeState.DistanceTravelled =
            Mathf.Clamp(
                routeState.DistanceTravelled,
                0f,
                totalRouteLength);

        float previousDistance =
            routeState.DistanceTravelled;

        float movementDistance =
            Mathf.Max(0f, npc.Speed) *
            GetTickScaledDeltaTime(deltaTime) *
            GetSpeedMultiplier();

        float nextDistance =
            Mathf.Clamp(
                previousDistance + movementDistance,
                0f,
                totalRouteLength);

        Vector3 oldPosition =
            npc.CurrentPosition;

        Vector3 oldFacingDirection =
            npc.FacingDirection;

        if (phaseStats != null)
            phaseStats.DistanceMathMs += EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        Vector3 newPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                nextDistance);

        if (phaseStats != null)
            phaseStats.PointOnPathMs += EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        Vector2 routeDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeState.Path,
                    nextDistance)
                : GetNpcDirectionOnPathAtDistance(
                    routeState.Path,
                    nextDistance);

        if (phaseStats != null)
            phaseStats.DirectionOnPathMs += EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        bool movedAlongRoute =
            nextDistance > previousDistance + 0.001f &&
            routeDirection.sqrMagnitude > DirectionThresholdSqrMagnitude;

        if (movedAlongRoute)
        {
            Vector3 movementDirection =
                new Vector3(
                    routeDirection.x,
                    routeDirection.y,
                    0f).normalized;

            npc.FacingDirection = movementDirection;
            npc.TickMovementDirection = movementDirection;

            float turnAngle =
                GetSignedAngle(oldFacingDirection, movementDirection);

            if (IsNpcTurnSpikeDebugEnabled() &&
                Mathf.Abs(turnAngle) >= GetMovementTurnSpikeAngleDegrees())
            {
                LogNpcMovementDebug(
                    "[NPC-TURN-SPIKE]" +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Type=" + npc.NpcType +
                    " | TurnAngle=" + turnAngle.ToString("0.###") +
                    " | OldPosition=" + FormatVector3(oldPosition) +
                    " | NewPosition=" + FormatVector3(newPosition) +
                    " | OldFacing=" + FormatVector3(oldFacingDirection) +
                    " | NewFacing=" + FormatVector3(movementDirection) +
                    " | RouteDirection=" + FormatVector2(routeDirection) +
                    " | PreviousDistance=" + previousDistance.ToString("0.###") +
                    " | NextDistance=" + nextDistance.ToString("0.###") +
                    " | TotalRouteLength=" + totalRouteLength.ToString("0.###") +
                    " | TargetPosition=" + FormatVector3(npc.TargetPosition));
            }
        }

        npc.CurrentPosition = newPosition;
        routeState.DistanceTravelled = nextDistance;

        npc.TravelProgress01 =
            Mathf.Clamp01(
                routeState.DistanceTravelled /
                totalRouteLength);

        if (phaseStats != null)
        {
            phaseStats.ApplyStateMs += EndPerfMeasureMs(phaseStartedAt);
            phaseStats.TickRouteMoveOuterMs += EndPerfMeasureMs(outerStartedAt);
        }

        outerStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        _eventBus.Publish(new SystemNpcPositionChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentSystemId,
            npc.CurrentPosition));

        if (phaseStats != null)
        {
            phaseStats.PublishMs += EndPerfMeasureMs(phaseStartedAt);
            phaseStats.TickPublishOuterMs += EndPerfMeasureMs(outerStartedAt);
            phaseStats.PublishedPositionChangedCount++;
        }

        outerStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (totalRouteLength - nextDistance <= arrivalThreshold)
        {
            phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            float distanceToRealTarget =
                Vector3.Distance(
                    npc.CurrentPosition,
                    npc.TargetPosition);

            if (phaseStats != null)
                phaseStats.ArrivalCheckMs += EndPerfMeasureMs(phaseStartedAt);

            if (distanceToRealTarget > arrivalThreshold)
            {
                phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

                ClearNpcMovementRoute(npc.RuntimeNpcId);

                if (phaseStats != null)
                {
                    phaseStats.ClearRouteMs += EndPerfMeasureMs(phaseStartedAt);
                    phaseStats.TickArrivalOrCompleteOuterMs += EndPerfMeasureMs(outerStartedAt);
                    phaseStats.ClearedRouteCount++;
                }

                return;
            }

            phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            bool completed =
                TryCompleteMovementOrDelaySystemExit(
                    npc,
                    currentTick,
                    phaseStats);

            if (phaseStats != null)
            {
                phaseStats.CompleteMs += EndPerfMeasureMs(phaseStartedAt);
                phaseStats.TickArrivalOrCompleteOuterMs += EndPerfMeasureMs(outerStartedAt);

                if (completed)
                    phaseStats.CompletedCount++;
            }
        }
        else if (phaseStats != null)
        {
            phaseStats.TickArrivalOrCompleteOuterMs += EndPerfMeasureMs(outerStartedAt);
        }
    }

    private Vector2 EnsureTickMovementDirection(SystemNpcRuntimeState npc, int currentTick)
    {
        return EnsureTickMovementDirection(npc, currentTick, null);
    }

    private Vector2 EnsureTickMovementDirection(
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long stepStartedAt;
        long entrySnapshotStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        if (phaseStats != null && npc != null)
        {
            phaseStats.EnsureDecision = "START";
            phaseStats.EnsureEntryRouteTargetKind = GetNpcRouteTargetKind(npc);
            phaseStats.EnsureEntryRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            phaseStats.EnsureEntryIsOnPlanet = npc.IsOnPlanet;
            phaseStats.EnsureEntryWaitingForInitialRouteBuild = npc.IsWaitingForInitialRouteBuild;
            phaseStats.EnsureEntryReleaseFromPlanetAfterInitialRouteBuild = npc.ReleaseFromPlanetAfterInitialRouteBuild;
            phaseStats.EnsureCurrentPlanetId = npc.CurrentPlanetId ?? string.Empty;
            phaseStats.EnsureTargetSystemId = npc.TargetSystemId ?? string.Empty;
            phaseStats.EnsureTargetPlanetId = npc.TargetPlanetId ?? string.Empty;
            phaseStats.EnsureTargetNpcId = npc.CurrentTargetRuntimeNpcId ?? string.Empty;
            phaseStats.EnsureEntryPosition = npc.CurrentPosition;

            if (!string.IsNullOrWhiteSpace(npc.RuntimeNpcId) &&
                _npcMovementRoutes.TryGetValue(npc.RuntimeNpcId, out NpcMovementRouteState entryRouteState) &&
                entryRouteState != null)
            {
                phaseStats.EnsureEntryHasActiveRoute =
                    entryRouteState.Path != null &&
                    entryRouteState.Path.Count > 1;

                phaseStats.EnsureEntryRoutePathCount =
                    entryRouteState.Path != null
                        ? entryRouteState.Path.Count
                        : 0;

                phaseStats.EnsureEntryHasSameContextRoute =
                    phaseStats.EnsureEntryHasActiveRoute &&
                    IsSameNpcMovementRouteContext(entryRouteState, npc);

                phaseStats.EnsureEntryRouteWasReusable =
                    phaseStats.EnsureEntryHasSameContextRoute &&
                    IsNpcFixedRouteStillReusableUntilArrival(npc, entryRouteState);
            }

            phaseStats.EnsureEntrySnapshotMs += EndPerfMeasureMs(entrySnapshotStartedAt);
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        if (TryUseReusableFixedRouteBeforeDestinationResolve(
            npc,
            currentTick,
            phaseStats,
            out Vector2 reusableFixedRouteDirection))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "USE_FIXED_ROUTE_FAST_BEFORE_DESTINATION_RESOLVE";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            return reusableFixedRouteDirection;
        }

        if (phaseStats != null)
            phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        Vector3 rawFinalTargetPosition =
            _routeService.GetNextTargetPosition(
                npc,
                phaseStats != null
                    ? phaseStats.TargetResolveStats
                    : null);

        rawFinalTargetPosition.z = -2f;

        if (phaseStats != null)
        {
            phaseStats.EnsureResolveTargetMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureRawTargetPosition = rawFinalTargetPosition;
        }

        Vector2 startFacingDirection =
            GetNpcSafeFacingDirection(npc);

        float boundaryStepDistance =
            GetNpcBoundaryRouteStepDistance(npc);

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        Vector3 finalTargetPosition =
            SystemBoundaryNavigation2A.GetRouteDestinationInsideSystemBounds(
                npc.CurrentSystemId,
                npc.CurrentPosition,
                rawFinalTargetPosition,
                startFacingDirection,
                _configService != null ? _configService.ShipMovementConfig : null,
                GetOrCreateNpcBoundaryNavigationState(npc.RuntimeNpcId),
                currentTick,
                boundaryStepDistance,
                npc.TravelState == SystemNpcTravelState.EngagingEnemy,
                out bool isBoundaryAdjusted);

        finalTargetPosition.z = -2f;
        npc.TargetPosition = finalTargetPosition;

        if (phaseStats != null)
        {
            phaseStats.EnsureBoundaryMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureBoundaryAdjusted = isBoundaryAdjusted;
            phaseStats.EnsureFinalTargetPosition = finalTargetPosition;
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                finalTargetPosition);

        float distanceToFinalTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                finalTargetPosition);

        if (phaseStats != null)
        {
            phaseStats.EnsureArrivalMathMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureArrivalThreshold = arrivalThreshold;
            phaseStats.EnsureDistanceToFinalTarget = distanceToFinalTarget;
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        if (distanceToFinalTarget <= arrivalThreshold &&
            TryHandleArrivalBeforeRouteBuild(
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureArrivalBeforeBuildMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "ARRIVAL_BEFORE_ROUTE_BUILD";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            return Vector2.zero;
        }

        if (phaseStats != null)
            phaseStats.EnsureArrivalBeforeBuildMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        bool hasActiveRoute =
            _npcMovementRoutes.TryGetValue(npc.RuntimeNpcId, out NpcMovementRouteState routeState) &&
            routeState != null &&
            routeState.Path != null &&
            routeState.Path.Count > 1 &&
            IsSameNpcMovementRouteContext(routeState, npc);

        NpcMovementRouteState existingRouteState =
            hasActiveRoute ? routeState : null;

        if (phaseStats != null)
        {
            phaseStats.EnsureActiveRouteLookupMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureEntryHasActiveRoute = hasActiveRoute;
            phaseStats.EnsureEntryHasSameContextRoute = hasActiveRoute;
            phaseStats.EnsureEntryRoutePathCount =
                routeState != null && routeState.Path != null
                    ? routeState.Path.Count
                    : phaseStats.EnsureEntryRoutePathCount;
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        if (hasActiveRoute &&
            IsNpcFixedRouteStillReusableUntilArrival(npc, routeState) &&
            TryUpdateNpcMovementTargetFromRoute(npc, routeState, currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "USE_FIXED_ROUTE_UNTIL_ARRIVAL";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                "USE_FIXED_ROUTE_UNTIL_ARRIVAL",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                true,
                true,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return npc.TickMovementDirection;
        }

        if (phaseStats != null)
            phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        float routeReuseDistanceThreshold =
            GetNpcRouteReuseDistanceThreshold(npc);

        bool hasReusableRoute =
            hasActiveRoute &&
            Vector3.Distance(routeState.Destination, finalTargetPosition) <= routeReuseDistanceThreshold;

        if (phaseStats != null)
        {
            phaseStats.EnsureRouteReuseDistanceMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureRouteReuseDistanceThreshold = routeReuseDistanceThreshold;
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        string offscreenMovingPlanetDecision;

        if (ShouldReuseOffscreenMovingPlanetRoute(
                npc,
                routeState,
                finalTargetPosition,
                currentTick,
                arrivalThreshold,
                hasActiveRoute,
                out offscreenMovingPlanetDecision) &&
            TryUpdateNpcMovementTargetFromRoute(
                npc,
                routeState,
                currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = offscreenMovingPlanetDecision;
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                offscreenMovingPlanetDecision,
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                true,
                false,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return npc.TickMovementDirection;
        }

        if (!string.IsNullOrWhiteSpace(offscreenMovingPlanetDecision) &&
            offscreenMovingPlanetDecision.StartsWith("REFRESH_", StringComparison.Ordinal))
        {
            LogNpcRouteDecisionPerf(
                offscreenMovingPlanetDecision,
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                hasActiveRoute,
                hasReusableRoute,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);
        }

        if (hasReusableRoute &&
            IsNpcFixedRouteStillReusable(npc, routeState, finalTargetPosition) &&
            TryUpdateNpcMovementTargetFromRoute(npc, routeState, currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "USE_FIXED_ROUTE_REUSE";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                "USE_FIXED_ROUTE_REUSE",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                true,
                true,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return npc.TickMovementDirection;
        }

        if (hasReusableRoute &&
            routeState.IsBoundaryEscapeRoute &&
            TryUpdateNpcMovementTargetFromRoute(npc, routeState, currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "USE_BOUNDARY_ROUTE_REUSE";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                "USE_BOUNDARY_ROUTE_REUSE",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                true,
                true,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return npc.TickMovementDirection;
        }

        if (hasActiveRoute &&
            IsNpcRouteRefreshBlockedByInitialTicks(npc, routeState, currentTick) &&
            TryUpdateNpcMovementTargetFromRoute(npc, routeState, currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "USE_INITIAL_TICK_BLOCKED_ROUTE";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                "USE_INITIAL_TICK_BLOCKED_ROUTE",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                true,
                hasReusableRoute,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return npc.TickMovementDirection;
        }

        if (hasReusableRoute &&
            ShouldKeepNpcRouteUntilArrival(npc) &&
            TryUpdateNpcMovementTargetFromRoute(npc, routeState, currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "USE_KEEP_UNTIL_ARRIVAL_ROUTE";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                "USE_KEEP_UNTIL_ARRIVAL_ROUTE",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                true,
                true,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return npc.TickMovementDirection;
        }

        if (hasActiveRoute &&
            routeState.Tick == currentTick &&
            TryUpdateNpcMovementTargetFromRoute(npc, routeState, currentTick))
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "SKIP_ALREADY_HAS_TICK_TARGET";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                "SKIP_ALREADY_HAS_TICK_TARGET",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                true,
                hasReusableRoute,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return npc.TickMovementDirection;
        }

        if (phaseStats != null)
            phaseStats.EnsureReuseChecksMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        bool shouldThrottleInitialRouteBuild =
            ShouldThrottleInitialRouteBuild(npc, hasActiveRoute);

        bool shouldBypassInitialRouteBuildLimit =
            ShouldBypassInitialRouteBuildLimit(npc);

        int initialRouteBuildsUsedBefore =
            _initialRouteBuildsUsedThisTick;

        int maxInitialRouteBuildsPerTick =
            GetMaxInitialRouteBuildsPerTick();

        if (phaseStats != null)
        {
            phaseStats.EnsureInitialRouteBuildsUsedBefore = initialRouteBuildsUsedBefore;
            phaseStats.EnsureMaxInitialRouteBuildsPerTick = maxInitialRouteBuildsPerTick;
        }

        if (shouldThrottleInitialRouteBuild &&
            !TryConsumeInitialRouteBuildSlot(currentTick))
        {
            MarkNpcWaitingForInitialRouteBuild(npc);

            if (phaseStats != null)
            {
                phaseStats.EnsureInitialLimitMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureInitialRouteBuildsUsedAfter = _initialRouteBuildsUsedThisTick;
                phaseStats.EnsureDecision = "WAIT_INITIAL_ROUTE_BUILD_LIMIT";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogInitialRouteBuildLimitDecision(
                "WAIT_INITIAL_ROUTE_BUILD_LIMIT",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                hasActiveRoute,
                hasReusableRoute,
                shouldThrottleInitialRouteBuild,
                shouldBypassInitialRouteBuildLimit,
                false,
                initialRouteBuildsUsedBefore,
                _initialRouteBuildsUsedThisTick,
                maxInitialRouteBuildsPerTick,
                currentTick);

            LogNpcRouteDecisionPerf(
                "WAIT_INITIAL_ROUTE_BUILD_LIMIT",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                hasActiveRoute,
                hasReusableRoute,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return Vector2.zero;
        }

        if (phaseStats != null)
        {
            phaseStats.EnsureInitialLimitMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureInitialRouteBuildsUsedAfter = _initialRouteBuildsUsedThisTick;
        }

        if (!hasActiveRoute)
        {
            LogInitialRouteBuildLimitDecision(
                "INITIAL_ROUTE_BUILD_ALLOWED",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                hasActiveRoute,
                hasReusableRoute,
                shouldThrottleInitialRouteBuild,
                shouldBypassInitialRouteBuildLimit,
                true,
                initialRouteBuildsUsedBefore,
                _initialRouteBuildsUsedThisTick,
                maxInitialRouteBuildsPerTick,
                currentTick);
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        NpcRouteBuildBudgetKind routeBuildBudgetKind =
            GetNpcRouteBuildBudgetKind(
                npc,
                hasActiveRoute);

        bool routeBuildBudgetExceeded =
            IsEnsureDirectionRouteBuildBudgetExceeded(
                routeBuildBudgetKind,
                currentTick,
                out string ensureBudgetDenyReason);

        if (phaseStats != null)
            phaseStats.EnsureBudgetCheckMs += EndPerfMeasureMs(stepStartedAt);

        if (routeBuildBudgetExceeded)
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureDecision = ensureBudgetDenyReason;
                phaseStats.EnsureBuiltRoute = false;
                phaseStats.EnsureBuiltRoutePathCount = 0;
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            if (existingRouteState != null &&
                TryUpdateNpcMovementTargetFromRoute(
                    npc,
                    existingRouteState,
                    currentTick))
            {
                if (phaseStats != null)
                    phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);

                LogNpcRouteDecisionPerf(
                    ensureBudgetDenyReason + "_KEEP_OLD_ROUTE",
                    npc,
                    rawFinalTargetPosition,
                    finalTargetPosition,
                    true,
                    hasReusableRoute,
                    isBoundaryAdjusted,
                    distanceToFinalTarget,
                    arrivalThreshold,
                    currentTick,
                    existingRouteState);

                return npc.TickMovementDirection;
            }

            if (phaseStats != null)
                phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);

            MarkNpcWaitingForInitialRouteBuild(npc);

            LogNpcRouteDecisionPerf(
                ensureBudgetDenyReason + "_WAIT",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                hasActiveRoute,
                hasReusableRoute,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return Vector2.zero;
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        bool enteredRouteBuildBudget =
            TryEnterRouteBuildBudget(
                routeBuildBudgetKind,
                currentTick,
                phaseStats,
                out string routeBuildBudgetDenyReason);

        if (phaseStats != null)
            phaseStats.EnsureBudgetEnterMs += EndPerfMeasureMs(stepStartedAt);

        if (!enteredRouteBuildBudget)
        {
            if (phaseStats != null)
            {
                phaseStats.EnsureDecision = routeBuildBudgetDenyReason;
                phaseStats.EnsureBuiltRoute = false;
                phaseStats.EnsureBuiltRoutePathCount = 0;
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            if (existingRouteState != null &&
                TryUpdateNpcMovementTargetFromRoute(
                    npc,
                    existingRouteState,
                    currentTick))
            {
                if (phaseStats != null)
                    phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);

                LogNpcRouteDecisionPerf(
                    routeBuildBudgetDenyReason + "_KEEP_OLD_ROUTE",
                    npc,
                    rawFinalTargetPosition,
                    finalTargetPosition,
                    true,
                    hasReusableRoute,
                    isBoundaryAdjusted,
                    distanceToFinalTarget,
                    arrivalThreshold,
                    currentTick,
                    existingRouteState);

                return npc.TickMovementDirection;
            }

            if (phaseStats != null)
                phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);

            MarkNpcWaitingForInitialRouteBuild(npc);

            LogNpcRouteDecisionPerf(
                routeBuildBudgetDenyReason + "_WAIT",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                hasActiveRoute,
                hasReusableRoute,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                routeState);

            return Vector2.zero;
        }

        _npcRouteWaypointsBuffer.Clear();

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        bool builtRoute =
            TryBuildNpcMovementRoutePathWithBudget(
                npc,
                finalTargetPosition,
                _npcRouteWaypointsBuffer,
                currentTick,
                routeBuildBudgetKind,
                out float routeDistanceTravelled,
                out double routeBuildElapsedMs);

        if (phaseStats != null)
        {
            phaseStats.EnsureBuildRouteMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureBuiltRoute = builtRoute;
            phaseStats.EnsureBuiltRoutePathCount = _npcRouteWaypointsBuffer.Count;
        }

        if (!builtRoute ||
            _npcRouteWaypointsBuffer.Count <= 1)
        {
            stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            string failedRouteTargetKind =
                GetNpcRouteTargetKind(npc);

            bool buildFailedByTimeBudget =
                WasLastNpcRouteBuildRejectedByTimeBudget();

            if (existingRouteState != null &&
                TryUpdateNpcMovementTargetFromRoute(
                    npc,
                    existingRouteState,
                    currentTick))
            {
                if (phaseStats != null)
                {
                    phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);
                    phaseStats.EnsureUsedExistingRouteAfterBuildFail = true;
                    phaseStats.EnsureDecision =
                        buildFailedByTimeBudget
                            ? "BUILD_TIME_BUDGET_EXCEEDED_KEEP_OLD_ROUTE"
                            : "BUILD_FAILED_KEEP_OLD_ROUTE";
                    phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                    phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
                }

                if (!buildFailedByTimeBudget)
                {
                    RerollNpcRouteTargetAfterBuildFailure(
                        npc,
                        failedRouteTargetKind,
                        finalTargetPosition,
                        currentTick,
                        phaseStats);
                }

                LogNpcRouteDecisionPerf(
                    buildFailedByTimeBudget
                        ? "BUILD_TIME_BUDGET_EXCEEDED_KEEP_OLD_ROUTE"
                        : "BUILD_FAILED_KEEP_OLD_ROUTE",
                    npc,
                    rawFinalTargetPosition,
                    finalTargetPosition,
                    true,
                    hasReusableRoute,
                    isBoundaryAdjusted,
                    distanceToFinalTarget,
                    arrivalThreshold,
                    currentTick,
                    existingRouteState);

                return npc.TickMovementDirection;
            }

            if (phaseStats != null)
                phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);

            stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            if (buildFailedByTimeBudget)
            {
                ClearNpcMovementRoute(npc.RuntimeNpcId);

                npc.CurrentMovementTargetPosition = npc.CurrentPosition;
                npc.TickMovementTargetPosition = npc.CurrentPosition;
                npc.TickMovementDirectionTick = currentTick;
                npc.TickMovementArrived = false;
                npc.TickMovementDirection = Vector3.zero;

                MarkNpcWaitingForInitialRouteBuild(npc);

                if (phaseStats != null)
                {
                    phaseStats.EnsureFallbackMs += EndPerfMeasureMs(stepStartedAt);
                    phaseStats.EnsureDecision = "BUILD_TIME_BUDGET_EXCEEDED_WAIT";
                    phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                    phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
                }

                LogNpcRouteDecisionPerf(
                    "BUILD_TIME_BUDGET_EXCEEDED_WAIT",
                    npc,
                    rawFinalTargetPosition,
                    finalTargetPosition,
                    false,
                    false,
                    isBoundaryAdjusted,
                    distanceToFinalTarget,
                    arrivalThreshold,
                    currentTick,
                    null);

                return Vector2.zero;
            }

            RerollNpcRouteTargetAfterBuildFailure(
                npc,
                failedRouteTargetKind,
                finalTargetPosition,
                currentTick,
                phaseStats);

            ClearNpcMovementRoute(npc.RuntimeNpcId);

            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = false;
            npc.TickMovementDirection = Vector3.zero;

            MarkNpcWaitingForInitialRouteBuild(npc);

            if (phaseStats != null)
            {
                phaseStats.EnsureFallbackMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = "BUILD_FAILED_WAIT_AFTER_TARGET_REROLL";
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                "BUILD_FAILED_WAIT_AFTER_TARGET_REROLL",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                false,
                false,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                null);

            return Vector2.zero;
        }

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        NpcMovementRouteState newRouteState =
            new NpcMovementRouteState
            {
                Destination = finalTargetPosition,
                SegmentIndex = 0,
                Tick = currentTick,
                BuildTick = currentTick,
                DistanceTravelled = Mathf.Max(0f, routeDistanceTravelled),
                TotalDistance = GetNpcPathLength(_npcRouteWaypointsBuffer),
                IsBoundaryEscapeRoute = isBoundaryAdjusted,
                RequiresStartTurnInPlace = false
            };

        newRouteState.Path.AddRange(_npcRouteWaypointsBuffer);

        RebuildNpcMovementRoutePlan(newRouteState, npc);

        UpdateNpcOffscreenMovingPlanetRouteState(
            newRouteState,
            npc,
            finalTargetPosition,
            currentTick);

        SaveNpcMovementRouteContext(newRouteState, npc);

        _npcMovementRoutes[npc.RuntimeNpcId] = newRouteState;

        if (phaseStats != null)
            phaseStats.EnsureCreateRouteStateMs += EndPerfMeasureMs(stepStartedAt);

        string buildDecision =
            newRouteState.StartTurnInPlacePending
                ? "BUILD_NEW_ROUTE_START_TURN_IN_PLACE"
                : "BUILD_NEW_ROUTE_OK";

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        if (TryUpdateNpcMovementTargetFromRoute(
                npc,
                newRouteState,
                currentTick))
        {
            ReleaseNpcAfterInitialRouteBuildIfNeeded(
                npc,
                currentTick,
                newRouteState);

            if (phaseStats != null)
            {
                phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureDecision = buildDecision;
                phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
                phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
            }

            LogNpcRouteDecisionPerf(
                buildDecision,
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                false,
                false,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                newRouteState);

            return npc.TickMovementDirection;
        }

        if (phaseStats != null)
            phaseStats.EnsureUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        ClearNpcMovementRoute(npc.RuntimeNpcId);

        npc.CurrentMovementTargetPosition = finalTargetPosition;
        npc.TickMovementTargetPosition = finalTargetPosition;
        npc.TickMovementDirectionTick = currentTick;
        npc.TickMovementArrived = false;

        Vector2 directDirection =
            (Vector2)(finalTargetPosition - npc.CurrentPosition);

        if (directDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
        {
            directDirection.Normalize();

            npc.TickMovementDirection =
                new Vector3(
                    directDirection.x,
                    directDirection.y,
                    0f);
        }

        if (phaseStats != null)
        {
            phaseStats.EnsureFallbackMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureDecision = "BUILD_NEW_ROUTE_BROKEN_FALLBACK_TO_DIRECT_TARGET";
            phaseStats.EnsureExitRouteTargetKind = GetNpcRouteTargetKind(npc);
            phaseStats.EnsureExitRouteTargetMoveKind = GetNpcRouteTargetMoveKind(npc);
        }

        LogNpcRouteDecisionPerf(
            "BUILD_NEW_ROUTE_BROKEN_FALLBACK_TO_DIRECT_TARGET",
            npc,
            rawFinalTargetPosition,
            finalTargetPosition,
            false,
            false,
            isBoundaryAdjusted,
            distanceToFinalTarget,
            arrivalThreshold,
            currentTick,
            newRouteState);

        return directDirection;
    }

    private void LogInitialRouteBuildLimitDecision(
        string decision,
        SystemNpcRuntimeState npc,
        Vector3 rawFinalTargetPosition,
        Vector3 finalTargetPosition,
        bool hasActiveRoute,
        bool hasReusableRoute,
        bool shouldThrottleInitialRouteBuild,
        bool shouldBypassInitialRouteBuildLimit,
        bool routeBuildWillRun,
        int initialRouteBuildsUsedBefore,
        int initialRouteBuildsUsedAfter,
        int maxInitialRouteBuildsPerTick,
        int currentTick)
    {
        if (npc == null ||
            Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.NpcMovementRouteDecision))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovementRouteDecision,
            "[SystemNpcMovementService] INITIAL_ROUTE_BUILD_LIMIT_DECISION " + decision +
            " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
            " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
            " | NpcSystemId=" + npc.CurrentSystemId +
            " | Npc=" + npc.RuntimeNpcId +
            " | Type=" + (npc.IsAlly ? "Ally" : npc.IsEnemy ? "Enemy" : "Npc") +
            " | IsAlly=" + npc.IsAlly +
            " | AllyRole=" + npc.AllyRole +
            " | IsHostileToPlayer=" + npc.IsHostileToPlayer +
            " | IsFighting=" + npc.IsFighting +
            " | Behavior=" + npc.CurrentBehavior +
            " | TravelState=" + npc.TravelState +
            GetNpcRouteTargetLogFields(npc) +
            " | Tick=" + currentTick +
            " | HasActiveRoute=" + hasActiveRoute +
            " | HasReusableRoute=" + hasReusableRoute +
            " | ShouldThrottleInitialRouteBuild=" + shouldThrottleInitialRouteBuild +
            " | ShouldBypassInitialRouteBuildLimit=" + shouldBypassInitialRouteBuildLimit +
            " | RouteBuildWillRun=" + routeBuildWillRun +
            " | InitialRouteBuildsUsedBefore=" + initialRouteBuildsUsedBefore +
            " | InitialRouteBuildsUsedAfter=" + initialRouteBuildsUsedAfter +
            " | MaxInitialRouteBuildsPerTick=" + maxInitialRouteBuildsPerTick +
            " | RawDestination=" + FormatVector3(rawFinalTargetPosition) +
            " | Destination=" + FormatVector3(finalTargetPosition) +
            " | Ms=0.00");
    }

    private bool TryUseReusableFixedRouteBeforeDestinationResolve(
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats,
        out Vector2 direction)
    {
        direction = Vector2.zero;

        long totalStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        long stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc == null ||
            string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
        {
            if (phaseStats != null)
                phaseStats.EnsureFastRouteTotalMs += EndPerfMeasureMs(totalStartedAt);

            return false;
        }

        bool hasRoute =
            _npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) &&
            routeState != null &&
            routeState.Path != null &&
            routeState.Path.Count > 1;

        if (phaseStats != null)
        {
            phaseStats.EnsureFastRouteLookupMs += EndPerfMeasureMs(stepStartedAt);

            if (routeState != null && routeState.Path != null)
                phaseStats.EnsureFastRoutePathPointCount = routeState.Path.Count;
        }

        if (!hasRoute)
        {
            if (phaseStats != null)
                phaseStats.EnsureFastRouteTotalMs += EndPerfMeasureMs(totalStartedAt);

            return false;
        }

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        bool isReusableUntilArrival =
            IsNpcFixedRouteStillReusableUntilArrival(npc, routeState);

        if (phaseStats != null)
            phaseStats.EnsureFastRouteReusableCheckMs += EndPerfMeasureMs(stepStartedAt);

        if (!isReusableUntilArrival)
        {
            if (phaseStats != null)
                phaseStats.EnsureFastRouteTotalMs += EndPerfMeasureMs(totalStartedAt);

            return false;
        }

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        bool updatedTarget =
            TryUpdateNpcMovementTargetFromRoute(
                npc,
                routeState,
                currentTick,
                phaseStats);

        if (phaseStats != null)
            phaseStats.EnsureFastRouteUpdateTargetMs += EndPerfMeasureMs(stepStartedAt);

        if (!updatedTarget)
        {
            if (phaseStats != null)
                phaseStats.EnsureFastRouteTotalMs += EndPerfMeasureMs(totalStartedAt);

            return false;
        }

        direction = npc.TickMovementDirection;

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                routeState.Destination);

        float distanceToDestination =
            Vector3.Distance(
                npc.CurrentPosition,
                routeState.Destination);

        if (phaseStats != null)
            phaseStats.EnsureFastRouteArrivalMathMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        LogNpcRouteDecisionPerf(
            "USE_FIXED_ROUTE_FAST_BEFORE_DESTINATION_RESOLVE",
            npc,
            routeState.Destination,
            routeState.Destination,
            true,
            true,
            routeState.IsBoundaryEscapeRoute,
            distanceToDestination,
            arrivalThreshold,
            currentTick,
            routeState);

        if (phaseStats != null)
        {
            phaseStats.EnsureFastRouteLogMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureFastRouteTotalMs += EndPerfMeasureMs(totalStartedAt);
        }

        return true;
    }

    private bool TryHandleArrivalBeforeRouteBuild(
        SystemNpcRuntimeState npc,
        Vector3 rawFinalTargetPosition,
        Vector3 finalTargetPosition,
        bool isBoundaryAdjusted,
        float distanceToFinalTarget,
        float arrivalThreshold,
        int currentTick)
    {
        if (npc == null)
            return false;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
        {
            ClearNpcMovementRoute(npc.RuntimeNpcId);
            ClearNpcInitialRouteBuildWait(npc);

            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirection = Vector3.zero;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = true;

            LogNpcRouteDecisionPerf(
                "ARRIVED_ENGAGING_ENEMY",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                false,
                false,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                null);

            return true;
        }

        if (npc.TravelState == SystemNpcTravelState.Patrolling)
        {
            ClearNpcMovementRoute(npc.RuntimeNpcId);
            ClearNpcInitialRouteBuildWait(npc);

            Vector3 nextPatrolPoint =
                _routeService.GetNextTargetPosition(npc);

            nextPatrolPoint.z = -2f;
            npc.TargetPosition = nextPatrolPoint;

            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirection = Vector3.zero;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = true;

            LogNpcRouteDecisionPerf(
                "ARRIVED_PATROL",
                npc,
                rawFinalTargetPosition,
                nextPatrolPoint,
                false,
                false,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                null);

            return true;
        }

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem ||
            npc.TravelState == SystemNpcTravelState.TravelingToAnotherSystem)
        {
            ClearNpcInitialRouteBuildWait(npc);

            npc.TargetPosition = finalTargetPosition;
            CompleteMovement(npc, currentTick);

            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirection = Vector3.zero;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = true;

            return true;
        }

        if (npc.CurrentBehavior == SystemNpcBehaviorType.None &&
            npc.TravelState == SystemNpcTravelState.Idle &&
            !npc.IsOnPlanet)
        {
            ClearNpcMovementRoute(npc.RuntimeNpcId);
            ClearNpcInitialRouteBuildWait(npc);

            npc.CurrentPosition = finalTargetPosition;
            npc.StartPosition = npc.CurrentPosition;
            npc.TargetPosition = Vector3.zero;
            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirection = Vector3.zero;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = true;
            npc.TravelProgress01 = 1f;

            LogNpcRouteDecisionPerf(
                "ARRIVED_IDLE_FIXED_MAP_POINT",
                npc,
                rawFinalTargetPosition,
                finalTargetPosition,
                false,
                false,
                isBoundaryAdjusted,
                distanceToFinalTarget,
                arrivalThreshold,
                currentTick,
                null);

            return true;
        }

        return false;
    }

    private void ClearNpcInitialRouteBuildWait(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        npc.IsWaitingForInitialRouteBuild = false;
        npc.ReleaseFromPlanetAfterInitialRouteBuild = false;
        npc.InitialRouteBuildPlanetId = null;
    }

    private bool ShouldReuseOffscreenMovingPlanetRoute(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        Vector3 currentMovingPlanetPosition,
        int currentTick,
        float arrivalThreshold,
        bool hasActiveRoute,
        out string decision)
    {
        decision = string.Empty;

        if (!IsNpcOffscreenMovingPlanetRoute(npc))
        {
            return false;
        }

        if (!IsOffscreenMovingPlanetRouteRefreshEnabled())
        {
            decision = "REFRESH_OFFSCREEN_MOVING_PLANET_DISABLED";
            return false;
        }

        if (!hasActiveRoute ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            decision = "REFRESH_OFFSCREEN_MOVING_PLANET_NO_ACTIVE_ROUTE";
            return false;
        }

        bool nearTerminal =
            IsOffscreenMovingPlanetRouteNearTerminal(
                npc,
                routeState,
                arrivalThreshold);

        bool driftTooLarge =
            IsOffscreenMovingPlanetDestinationDriftTooLarge(
                routeState,
                currentMovingPlanetPosition);

        bool cooldownActive =
            IsOffscreenMovingPlanetRefreshCooldownActive(
                routeState,
                currentTick);

        if (nearTerminal)
        {
            if (cooldownActive)
            {
                decision = "USE_OFFSCREEN_MOVING_PLANET_TERMINAL_COOLDOWN";
                return true;
            }

            decision = "REFRESH_OFFSCREEN_MOVING_PLANET_TERMINAL";
            return false;
        }

        if (driftTooLarge)
        {
            if (cooldownActive)
            {
                decision = "USE_OFFSCREEN_MOVING_PLANET_DRIFT_COOLDOWN";
                return true;
            }

            decision = "REFRESH_OFFSCREEN_MOVING_PLANET_DRIFT";
            return false;
        }

        decision = "USE_OFFSCREEN_MOVING_PLANET_SNAPSHOT_ROUTE";
        return true;
    }

    private bool IsNpcOffscreenMovingPlanetRoute(SystemNpcRuntimeState npc)
    {
        if (npc == null)
        {
            return false;
        }

        if (IsCurrentSystemForPerf(npc.CurrentSystemId))
        {
            return false;
        }

        return string.Equals(
            GetNpcRouteTargetKind(npc),
            "MOVING_PLANET",
            StringComparison.Ordinal);
    }

    private bool IsOffscreenMovingPlanetRouteRefreshEnabled()
    {
        ShipMovementConfig movementConfig = _configService?.ShipMovementConfig;
        return movementConfig == null || movementConfig.OffscreenMovingPlanetRouteRefreshEnabled;
    }

    private int GetOffscreenMovingPlanetRouteRefreshCooldownTicks()
    {
        ShipMovementConfig movementConfig = _configService?.ShipMovementConfig;
        return movementConfig != null
            ? movementConfig.OffscreenMovingPlanetRouteRefreshCooldownTicks
            : 10;
    }

    private float GetOffscreenMovingPlanetTerminalRefreshDistance()
    {
        ShipMovementConfig movementConfig = _configService?.ShipMovementConfig;
        return movementConfig != null
            ? movementConfig.OffscreenMovingPlanetTerminalRefreshDistance
            : 80f;
    }

    private int GetOffscreenMovingPlanetTerminalRefreshTimeTicks()
    {
        ShipMovementConfig movementConfig = _configService?.ShipMovementConfig;
        return movementConfig != null
            ? movementConfig.OffscreenMovingPlanetTerminalRefreshTimeTicks
            : 1;
    }

    private float GetOffscreenMovingPlanetMaxDestinationDriftBeforeRefresh()
    {
        ShipMovementConfig movementConfig = _configService?.ShipMovementConfig;
        return movementConfig != null
            ? movementConfig.OffscreenMovingPlanetMaxDestinationDriftBeforeRefresh
            : 300f;
    }

    private bool IsOffscreenMovingPlanetRefreshCooldownActive(
        NpcMovementRouteState routeState,
        int currentTick)
    {
        if (routeState == null || routeState.OffscreenMovingPlanetLastRefreshTick < 0)
        {
            return false;
        }

        int cooldownTicks = GetOffscreenMovingPlanetRouteRefreshCooldownTicks();
        if (cooldownTicks <= 0)
        {
            return false;
        }

        return currentTick - routeState.OffscreenMovingPlanetLastRefreshTick < cooldownTicks;
    }

    private bool IsOffscreenMovingPlanetRouteNearTerminal(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        float arrivalThreshold)
    {
        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        float terminalDistance =
            Mathf.Max(
                arrivalThreshold,
                GetOffscreenMovingPlanetTerminalRefreshDistance());

        float remainingDistance =
            Mathf.Max(
                0f,
                routeState.TotalDistance - routeState.DistanceTravelled);

        if (remainingDistance <= terminalDistance)
        {
            return true;
        }

        int terminalTicks =
            GetOffscreenMovingPlanetTerminalRefreshTimeTicks();

        if (terminalTicks <= 0)
        {
            return false;
        }

        float speedPerTick =
            Mathf.Max(0f, npc.Speed) *
            GetSpeedMultiplier();

        if (speedPerTick <= DirectionThresholdSqrMagnitude)
        {
            return false;
        }

        return remainingDistance <= speedPerTick * terminalTicks;
    }

    private bool IsOffscreenMovingPlanetDestinationDriftTooLarge(
        NpcMovementRouteState routeState,
        Vector3 currentMovingPlanetPosition)
    {
        if (routeState == null)
        {
            return false;
        }

        float maxDrift = GetOffscreenMovingPlanetMaxDestinationDriftBeforeRefresh();
        if (maxDrift <= 0f)
        {
            return false;
        }

        Vector3 snapshotPosition = routeState.OffscreenMovingPlanetSnapshotDestination;
        snapshotPosition.z = currentMovingPlanetPosition.z;

        return Vector3.Distance(snapshotPosition, currentMovingPlanetPosition) > maxDrift;
    }

    private void UpdateNpcOffscreenMovingPlanetRouteState(
        NpcMovementRouteState routeState,
        SystemNpcRuntimeState npc,
        Vector3 snapshotDestination,
        int currentTick)
    {
        if (routeState == null)
        {
            return;
        }

        bool isOffscreenMovingPlanetRoute = IsNpcOffscreenMovingPlanetRoute(npc);
        routeState.IsOffscreenMovingPlanetRoute = isOffscreenMovingPlanetRoute;

        if (!isOffscreenMovingPlanetRoute)
        {
            routeState.OffscreenMovingPlanetLastRefreshTick = -1;
            routeState.OffscreenMovingPlanetSnapshotDestination = Vector3.zero;
            routeState.OffscreenMovingPlanetSnapshotTargetPlanetId = string.Empty;
            return;
        }

        routeState.OffscreenMovingPlanetLastRefreshTick = currentTick;
        routeState.OffscreenMovingPlanetSnapshotDestination = snapshotDestination;
        routeState.OffscreenMovingPlanetSnapshotTargetPlanetId = npc.TargetPlanetId ?? string.Empty;
    }

    private bool IsNpcBoundaryRouteStillUseful(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        Vector3 finalTargetPosition)
    {
        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        if (!SystemBoundaryNavigation2A.IsPositionNearSystemBounds(
                npc.CurrentSystemId,
                finalTargetPosition,
                _configService != null ? _configService.ShipMovementConfig : null))
        {
            return false;
        }

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                finalTargetPosition);

        if (totalRouteLength <= arrivalThreshold)
            return false;

        return routeState.DistanceTravelled <
               totalRouteLength - arrivalThreshold;
    }

    private bool TryUpdateNpcMovementTargetFromRoute(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        int currentTick)
    {
        return TryUpdateNpcMovementTargetFromRoute(
            npc,
            routeState,
            currentTick,
            null);
    }

    private bool TryUpdateNpcMovementTargetFromRoute(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            if (phaseStats != null)
                phaseStats.EnsureUpdateTargetPrecheckMs += EndPerfMeasureMs(stepStartedAt);

            return false;
        }

        routeState.Tick = currentTick;

        if (phaseStats != null)
            phaseStats.EnsureUpdateTargetPrecheckMs += EndPerfMeasureMs(stepStartedAt);

        if (routeState.StartTurnInPlacePending)
        {
            stepStartedAt =
                phaseStats != null
                    ? BeginPerfMeasure()
                    : 0L;

            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = false;

            if (phaseStats != null)
                phaseStats.EnsureUpdateTargetStartTurnMs += EndPerfMeasureMs(stepStartedAt);

            return true;
        }

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        if (phaseStats != null)
            phaseStats.EnsureUpdateTargetPathLengthMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                routeState.Destination);

        if (phaseStats != null)
            phaseStats.EnsureUpdateTargetArrivalThresholdMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (routeState.DistanceTravelled >= totalRouteLength - arrivalThreshold)
        {
            if (phaseStats != null)
                phaseStats.EnsureUpdateTargetDistanceMathMs += EndPerfMeasureMs(stepStartedAt);

            return false;
        }

        float distancePerTick =
            Mathf.Max(0f, npc.Speed) *
            GetSpeedMultiplier();

        float nextPreviewDistance =
            Mathf.Clamp(
                routeState.DistanceTravelled + distancePerTick,
                0f,
                totalRouteLength);

        if (phaseStats != null)
            phaseStats.EnsureUpdateTargetDistanceMathMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        Vector3 movementTargetPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                nextPreviewDistance);

        if (phaseStats != null)
            phaseStats.EnsureUpdateTargetPointOnPathMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        npc.CurrentMovementTargetPosition = movementTargetPosition;
        npc.TickMovementTargetPosition = movementTargetPosition;
        npc.TickMovementDirectionTick = currentTick;
        npc.TickMovementArrived = false;

        if (phaseStats != null)
            phaseStats.EnsureUpdateTargetAssignMs += EndPerfMeasureMs(stepStartedAt);

        return true;
    }

    private float GetSpeedMultiplier()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 1f;

        return config.SpeedMultiplier;
    }

    private bool IsNpcRouteRefreshBlockedByInitialTicks(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        int currentTick)
    {
        if (IsNpcFinalPlanetApproachRoutePriorityCandidate(npc))
            return false;

        if (!ShouldUseNpcRouteLockedPrefix(npc) ||
            routeState == null)
        {
            return false;
        }

        int blockedTicks =
            GetNpcRouteRefreshBlockedInitialTicks();

        if (blockedTicks <= 0)
            return false;

        if (routeState.BuildTick < 0)
            return false;

        int passedTicks =
            currentTick - routeState.BuildTick;

        if (passedTicks < 0)
            return false;

        return passedTicks < blockedTicks;
    }

    private int GetNpcRouteRefreshBlockedInitialTicks()
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return movementConfig != null
            ? movementConfig.MovingDestinationRouteRefreshBlockedInitialTicks
            : 1;
    }

    private bool ShouldKeepNpcRouteUntilArrival(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (npc.TravelState == SystemNpcTravelState.Patrolling)
            return true;

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem &&
            npc.CurrentBehavior == SystemNpcBehaviorType.PlanetToPlanetTravel)
        {
            return true;
        }

        if (npc.TravelState == SystemNpcTravelState.TravelingToAnotherSystem)
            return true;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return true;

        return false;
    }

    private float GetNpcRouteReuseDistanceThreshold(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return ArrivalDistanceThreshold;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return 40f;

        return ArrivalDistanceThreshold;
    }

    public bool TryBuildRoutePreview2A(
        string runtimeNpcId,
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick)
    {
        if (preview == null)
        {
            LogCustom("[NpcRouteDebug] BuildPreview failed: preview is null.");
            return false;
        }

        preview.Clear();

        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return false;

        if (_runtimeService == null)
            return false;

        if (_shipRouteService == null)
            return false;

        if (!_runtimeService.TryGetNpc(
                runtimeNpcId,
                out SystemNpcRuntimeState npc))
        {
            return false;
        }

        if (npc == null ||
            !npc.IsAlive ||
            npc.IsOnPlanet)
        {
            return false;
        }

        if (!TryGetNpcPreviewDestination(
                npc,
                out Vector3 destinationPosition))
        {
            return false;
        }

        IReadOnlyList<Vector3> path;
        float passedDistance;

        if (TryGetActiveNpcPreviewRoute(
                npc,
                destinationPosition,
                out NpcMovementRouteState activeRouteState))
        {
            path = activeRouteState.Path;

            passedDistance =
                Mathf.Clamp(
                    activeRouteState.DistanceTravelled,
                    0f,
                    GetNpcPathLength(activeRouteState.Path));
        }
        else
        {
            if (!TryBuildNpcMovementRoutePath(
                    npc,
                    destinationPosition,
                    _npcRoutePreviewPathBuffer,
                    -1,
                    out float previewRouteDistanceTravelled))
            {
                return false;
            }

            path = _npcRoutePreviewPathBuffer;

            passedDistance =
                GetClosestDistanceOnNpcPath(
                    path,
                    npc.CurrentPosition);
        }

        return _shipRouteService.FillPreviewFromPath(
            path,
            Mathf.Max(0.01f, npc.Speed * GetSpeedMultiplier()),
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            secondsPerTick,
            passedDistance,
            GetCurrentTickRemainingFactor());
    }

    private float GetClosestDistanceOnNpcPath(
    IReadOnlyList<Vector3> path,
    Vector3 position)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        float bestDistanceOnPath = 0f;
        float bestSqrDistance = float.MaxValue;
        float travelledDistance = 0f;

        Vector2 position2 =
            new Vector2(
                position.x,
                position.y);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            Vector2 from2 =
                new Vector2(
                    from.x,
                    from.y);

            Vector2 to2 =
                new Vector2(
                    to.x,
                    to.y);

            Vector2 segment =
                to2 - from2;

            float segmentLength =
                segment.magnitude;

            if (segmentLength <= DirectionThresholdSqrMagnitude)
                continue;

            float t =
                Vector2.Dot(
                    position2 - from2,
                    segment) /
                Mathf.Max(
                    DirectionThresholdSqrMagnitude,
                    segment.sqrMagnitude);

            t = Mathf.Clamp01(t);

            Vector2 closestPoint =
                from2 + segment * t;

            float sqrDistance =
                (position2 - closestPoint).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestDistanceOnPath =
                    travelledDistance +
                    segmentLength * t;
            }

            travelledDistance += segmentLength;
        }

        return Mathf.Max(0f, bestDistanceOnPath);
    }

    private bool TryGetNpcPreviewDestination(
    SystemNpcRuntimeState npc,
    out Vector3 destinationPosition)
    {
        destinationPosition = Vector3.zero;

        if (npc == null)
            return false;

        Vector3 liveTargetPosition =
            _routeService != null
                ? _routeService.GetNextTargetPosition(npc)
                : Vector3.zero;

        if (TryUseNpcPreviewDestination(
                npc,
                liveTargetPosition,
                out destinationPosition))
        {
            return true;
        }

        if (TryUseNpcPreviewDestination(
                npc,
                npc.TargetPosition,
                out destinationPosition))
        {
            return true;
        }

        if (TryUseNpcPreviewDestination(
                npc,
                npc.CurrentMovementTargetPosition,
                out destinationPosition))
        {
            return true;
        }

        if (TryUseNpcPreviewDestination(
                npc,
                npc.TickMovementTargetPosition,
                out destinationPosition))
        {
            return true;
        }

        return false;
    }

    private bool TryUseNpcPreviewDestination(
    SystemNpcRuntimeState npc,
    Vector3 candidatePosition,
    out Vector3 destinationPosition)
    {
        destinationPosition = Vector3.zero;

        if (npc == null)
            return false;

        if (!IsFinite(candidatePosition))
            return false;

        if (candidatePosition == Vector3.zero)
            return false;

        candidatePosition.z = npc.CurrentPosition.z;

        if (Vector3.Distance(
                npc.CurrentPosition,
                candidatePosition) <= ArrivalDistanceThreshold)
        {
            return false;
        }

        destinationPosition = candidatePosition;
        return true;
    }

    private static bool IsFinite(
    Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(
        float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private bool TryBuildNpcMovementRoutePath(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition,
        List<Vector3> routePath,
        int currentTick,
        out float routeDistanceTravelled)
    {
        long startedAt = BeginPerfMeasure();

        routeDistanceTravelled = 0f;

        if (npc == null || routePath == null)
            return false;

        routePath.Clear();

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                destinationPosition);

        if (Vector3.Distance(
                npc.CurrentPosition,
                destinationPosition) <= arrivalThreshold)
        {
            double elapsedMs = EndPerfMeasureMs(startedAt);

            if (ShouldLogNpcPerf(elapsedMs))
            {
                LogNpcPerf(
                    elapsedMs,
                    "SystemNpcMovementService.TryBuildNpcMovementRoutePath SKIP_ARRIVAL" +
                    " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
                    " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
                    " | NpcSystemId=" + npc.CurrentSystemId +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Behavior=" + npc.CurrentBehavior +
                    " | TravelState=" + npc.TravelState +
                    GetNpcRouteTargetLogFields(npc) +
                    " | Destination=" + FormatVector3(destinationPosition));
            }

            return false;
        }

        if (_shipRouteService == null)
            return false;

        long preservedStartedAt = BeginPerfMeasure();

        if (TryBuildNpcRouteWithPreservedPrefix(
                npc,
                destinationPosition,
                arrivalThreshold,
                routePath,
                currentTick,
                out routeDistanceTravelled))
        {
            double elapsedMs = EndPerfMeasureMs(startedAt);

            if (ShouldLogNpcPerf(elapsedMs))
            {
                LogNpcPerf(
                    elapsedMs,
                    "SystemNpcMovementService.TryBuildNpcMovementRoutePath OK preservedPrefix" +
                    " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
                    " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
                    " | NpcSystemId=" + npc.CurrentSystemId +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Behavior=" + npc.CurrentBehavior +
                    " | TravelState=" + npc.TravelState +
                    GetNpcRouteTargetLogFields(npc) +
                    " | PathCount=" + routePath.Count +
                    " | Destination=" + FormatVector3(destinationPosition) +
                    " | PreservedMs=" + EndPerfMeasureMs(preservedStartedAt).ToString("F2"));
            }

            return true;
        }

        Vector2 startFacingDirection =
            GetNpcSafeFacingDirection(npc);

        routeDistanceTravelled = 0f;

        long buildStartedAt = BeginPerfMeasure();

        bool built =
            TryBuildNpcRoutePathFrom(
                npc,
                npc.CurrentPosition,
                destinationPosition,
                startFacingDirection,
                arrivalThreshold,
                routePath,
                currentTick);

        double totalMs = EndPerfMeasureMs(startedAt);

        if (ShouldLogNpcPerf(totalMs))
        {
            LogNpcPerf(
                totalMs,
                "SystemNpcMovementService.TryBuildNpcMovementRoutePath " + (built ? "OK" : "FAILED") +
                " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
                " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
                " | NpcSystemId=" + npc.CurrentSystemId +
                " | Npc=" + npc.RuntimeNpcId +
                " | Behavior=" + npc.CurrentBehavior +
                " | TravelState=" + npc.TravelState +
                GetNpcRouteTargetLogFields(npc) +
                GetNpcRoutePipelineLogFields(npc) +
                " | PathCount=" + routePath.Count +
                " | Destination=" + FormatVector3(destinationPosition) +
                " | PreservedMs=" + EndPerfMeasureMs(preservedStartedAt).ToString("F2") +
                " | BuildFromMs=" + EndPerfMeasureMs(buildStartedAt).ToString("F2"));
        }

        return built;
    }

    private bool TryBuildNpcRoutePathFrom(
        SystemNpcRuntimeState npc,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        float arrivalThreshold,
        List<Vector3> routePath,
        int currentTick)
    {
        long totalStartedAt = BeginPerfMeasure();

        if (npc == null || routePath == null)
            return false;

        routePath.Clear();

        float boundaryStepDistance =
            GetNpcBoundaryRouteStepDistance(npc);

        long boundaryStartedAt = BeginPerfMeasure();

        Vector3 adjustedDestinationPosition =
            SystemBoundaryNavigation2A.GetRouteDestinationInsideSystemBounds(
                npc.CurrentSystemId,
                startPosition,
                destinationPosition,
                startFacingDirection,
                _configService != null ? _configService.ShipMovementConfig : null,
                GetOrCreateNpcBoundaryNavigationState(npc.RuntimeNpcId),
                currentTick,
                boundaryStepDistance,
                npc.TravelState == SystemNpcTravelState.EngagingEnemy,
                out bool isBoundaryAdjusted);

        double boundaryMs = EndPerfMeasureMs(boundaryStartedAt);

        adjustedDestinationPosition.z = -2f;

        float adjustedDistance =
            Vector3.Distance(
                startPosition,
                adjustedDestinationPosition);

        if (adjustedDistance <= arrivalThreshold)
        {
            double elapsedMs = EndPerfMeasureMs(totalStartedAt);

            if (ShouldLogNpcPerf(elapsedMs))
            {
                LogNpcPerf(
                    elapsedMs,
                    "SystemNpcMovementService.TryBuildNpcRoutePathFrom SKIP_ARRIVAL" +
                    " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
                    " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
                    " | NpcSystemId=" + npc.CurrentSystemId +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Behavior=" + npc.CurrentBehavior +
                    " | TravelState=" + npc.TravelState +
                    GetNpcRouteTargetLogFields(npc) +
                    GetNpcRoutePipelineLogFields(npc) +
                    " | BoundaryAdjusted=" + isBoundaryAdjusted +
                    " | BoundaryMs=" + boundaryMs.ToString("F2") +
                    " | Destination=" + FormatVector3(destinationPosition) +
                    " | AdjustedDestination=" + FormatVector3(adjustedDestinationPosition) +
                    " | AdjustedDistance=" + adjustedDistance.ToString("0.###") +
                    " | ArrivalThreshold=" + arrivalThreshold.ToString("0.###"));
            }

            return false;
        }

        SystemShipRouteRequest2A request =
            new SystemShipRouteRequest2A
            {
                SystemId = npc.CurrentSystemId,
                StartPosition = startPosition,
                DestinationPosition = adjustedDestinationPosition,
                StartFacingDirection = startFacingDirection,
                TargetKind = GetNpcShipRouteTargetKind2A(npc),
                Settings = CreateNpcRouteSettings(
                    npc,
                    arrivalThreshold,
                    false)
            };

        long buildStartedAt = BeginPerfMeasure();

        bool routeBuilt =
            _shipRouteService != null &&
            _shipRouteService.TryBuildRoute(
                request,
                _npcRouteBuildResult);

        double buildMs = EndPerfMeasureMs(buildStartedAt);

        int builtPathCount =
            _npcRouteBuildResult.Path != null
                ? _npcRouteBuildResult.Path.Count
                : 0;

        if (!routeBuilt ||
            _npcRouteBuildResult.Path == null ||
            _npcRouteBuildResult.Path.Count <= 1)
        {
            double elapsedMs = EndPerfMeasureMs(totalStartedAt);

            if (ShouldLogNpcPerf(elapsedMs))
            {
                string routeCompareLogFields =
                    BuildNpcRoutePerfComparisonLogFields(
                        npc,
                        adjustedDestinationPosition,
                        _npcRouteBuildResult.Path,
                        _npcRouteBuildResult.PathLength);

                LogNpcPerf(
                    elapsedMs,
                    "SystemNpcMovementService.TryBuildNpcRoutePathFrom BUILD_FAILED" +
                    " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
                    " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
                    " | NpcSystemId=" + npc.CurrentSystemId +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Behavior=" + npc.CurrentBehavior +
                    " | TravelState=" + npc.TravelState +
                    GetNpcRouteTargetLogFields(npc) +
                    GetNpcRoutePipelineLogFields(npc) +
                    routeCompareLogFields +
                    BuildNpcShipRouteResultLogFields(_npcRouteBuildResult) +
                    " | BoundaryAdjusted=" + isBoundaryAdjusted +
                    " | BoundaryMs=" + boundaryMs.ToString("F2") +
                    " | ShipRouteBuildMs=" + buildMs.ToString("F2") +
                    " | RouteBuilt=" + routeBuilt +
                    " | PathCount=" + builtPathCount +
                    " | Destination=" + FormatVector3(destinationPosition) +
                    " | AdjustedDestination=" + FormatVector3(adjustedDestinationPosition) +
                    " | PathLength=" + _npcRouteBuildResult.PathLength.ToString("0.###"));
            }

            return false;
        }

        routePath.AddRange(_npcRouteBuildResult.Path);

        double totalMs = EndPerfMeasureMs(totalStartedAt);

        if (ShouldLogNpcPerf(totalMs))
        {
            string routeCompareLogFields =
                BuildNpcRoutePerfComparisonLogFields(
                    npc,
                    adjustedDestinationPosition,
                    _npcRouteBuildResult.Path,
                    _npcRouteBuildResult.PathLength);

            LogNpcPerf(
                totalMs,
                "SystemNpcMovementService.TryBuildNpcRoutePathFrom OK" +
                " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
                " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
                " | NpcSystemId=" + npc.CurrentSystemId +
                " | Npc=" + npc.RuntimeNpcId +
                " | Behavior=" + npc.CurrentBehavior +
                " | TravelState=" + npc.TravelState +
                GetNpcRouteTargetLogFields(npc) +
                GetNpcRoutePipelineLogFields(npc) +
                routeCompareLogFields +
                BuildNpcShipRouteResultLogFields(_npcRouteBuildResult) +
                " | BoundaryAdjusted=" + isBoundaryAdjusted +
                " | BoundaryMs=" + boundaryMs.ToString("F2") +
                " | ShipRouteBuildMs=" + buildMs.ToString("F2") +
                " | PathCount=" + routePath.Count +
                " | Destination=" + FormatVector3(destinationPosition) +
                " | AdjustedDestination=" + FormatVector3(adjustedDestinationPosition) +
                " | PathLength=" + _npcRouteBuildResult.PathLength.ToString("0.###"));
        }

        return true;
    }

    private SystemShipRouteTargetKind2A GetNpcShipRouteTargetKind2A(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return SystemShipRouteTargetKind2A.MapPoint;

        if (npc.IsEnemy)
            return SystemShipRouteTargetKind2A.Enemy;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy ||
            !string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
        {
            return SystemShipRouteTargetKind2A.Npc;
        }

        string routeTargetKind =
            GetNpcRouteTargetKind(npc);

        switch (routeTargetKind)
        {
            case "MOVING_PLANET":
                return SystemShipRouteTargetKind2A.Planet;

            case "FIXED_SYSTEM_EXIT_POINT":
                return SystemShipRouteTargetKind2A.SystemExit;

            case "FIXED_PATROL_POINT":
            case "FIXED_MAP_POINT":
                return SystemShipRouteTargetKind2A.MapPoint;

            case "MOVING_NPC_OR_ENEMY":
                return SystemShipRouteTargetKind2A.Npc;

            default:
                return SystemShipRouteTargetKind2A.MapPoint;
        }
    }

    private float GetNpcBoundaryRouteStepDistance(
        SystemNpcRuntimeState npc)
    {
        float speed =
            npc != null
                ? Mathf.Max(0f, npc.Speed)
                : 0f;

        return Mathf.Clamp(
            speed * 2.5f,
            ArrivalDistanceThreshold * 4f,
            260f);
    }

    private bool ShouldRejectNpcStartTurnSpikeRoute(
        SystemNpcRuntimeState npc,
        Vector2 startFacingDirection,
        SystemShipRouteResult2A routeResult)
    {
        if (npc == null ||
            routeResult == null ||
            routeResult.Path == null ||
            routeResult.Path.Count <= 1 ||
            startFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return false;
        }

        Vector2 routeStartDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f)
                : GetNpcDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f);

        if (routeStartDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        float startTurnAngle =
            Mathf.Abs(
                Vector2.SignedAngle(
                    startFacingDirection.normalized,
                    routeStartDirection.normalized));

        float maxAllowedStartTurnAngle =
            Mathf.Clamp(
                GetMovementTurnSpikeAngleDegrees(),
                1f,
                179f);

        bool shouldReject =
            startTurnAngle > maxAllowedStartTurnAngle;

        if (shouldReject &&
            ShouldLogNpcRouteDecision())
        {
            LogNpcMovementDebug(
                "[NPC-ROUTE-START-SPIKE-REJECTED]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | StartTurnAngle=" + startTurnAngle.ToString("0.###") +
                " | MaxAllowed=" + maxAllowedStartTurnAngle.ToString("0.###") +
                " | StartFacing=" + FormatVector2(startFacingDirection) +
                " | RouteDirection=" + FormatVector2(routeStartDirection) +
                " | PathLength=" + routeResult.PathLength.ToString("0.###") +
                " | UsedRawSafeFallback=" + routeResult.UsedRawSafeFallback +
                " | UsedSunAvoidance=" + routeResult.UsedSunAvoidance +
                " | Destination=" + FormatVector3(routeResult.Path[routeResult.Path.Count - 1]));
        }

        return shouldReject;
    }

    private bool TryBuildNpcRouteWithPreservedPrefix(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition,
        float arrivalThreshold,
        List<Vector3> routePath,
        int currentTick,
        out float routeDistanceTravelled)
    {
        routeDistanceTravelled = 0f;

        if (!ShouldUseNpcRouteLockedPrefix(npc) ||
            routePath == null)
        {
            return false;
        }

        if (!_npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1 ||
            !IsSameNpcMovementRouteContext(routeState, npc))
        {
            return false;
        }

        if (!TryGetNpcLockedPrefixState(
                npc,
                routeState,
                out float currentDistance,
                out float lockedPrefixDistance,
                out Vector3 lockedPrefixEndPosition,
                out Vector2 lockedPrefixEndFacingDirection))
        {
            return false;
        }

        if (!TryBuildNpcRoutePathFrom(
                npc,
                lockedPrefixEndPosition,
                destinationPosition,
                lockedPrefixEndFacingDirection,
                arrivalThreshold,
                _npcRouteTailPathBuffer,
                currentTick))
        {
            return false;
        }

        BuildNpcRoutePrefix(
            routeState.Path,
            lockedPrefixDistance,
            _npcRouteSegmentPathBuffer);

        if (_npcRouteSegmentPathBuffer.Count <= 1)
            return false;

        routePath.Clear();
        routePath.AddRange(_npcRouteSegmentPathBuffer);

        AppendNpcRouteTail(
            _npcRouteTailPathBuffer,
            routePath);

        if (routePath.Count <= 1)
            return false;

        routeDistanceTravelled =
            Mathf.Min(
                currentDistance,
                lockedPrefixDistance);

        if (ShouldLogNpcRouteDecision())
        {
            LogNpcMovementDebug(
                "[NPC-ROUTE-PREFIX-PRESERVED]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | CurrentDistance=" + currentDistance.ToString("0.###") +
                " | LockedPrefixDistance=" + lockedPrefixDistance.ToString("0.###") +
                " | PreservedDistance=" + routeDistanceTravelled.ToString("0.###") +
                " | PrefixEnd=" + FormatVector3(lockedPrefixEndPosition) +
                " | PrefixFacing=" + FormatVector2(lockedPrefixEndFacingDirection) +
                " | Destination=" + FormatVector3(destinationPosition) +
                " | PathCount=" + routePath.Count);
        }

        return true;
    }

    private bool TryGetNpcLockedPrefixState(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        out float currentDistance,
        out float lockedPrefixDistance,
        out Vector3 lockedPrefixEndPosition,
        out Vector2 lockedPrefixEndFacingDirection)
    {
        currentDistance = 0f;
        lockedPrefixDistance = 0f;
        lockedPrefixEndPosition = Vector3.zero;
        lockedPrefixEndFacingDirection = Vector2.up;

        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        int lockedPrefixSlots =
            GetNpcRouteLockedPrefixSlots();

        if (lockedPrefixSlots <= 0)
            return false;

        float totalPathLength =
            GetNpcPathLength(routeState.Path);

        if (totalPathLength <= ArrivalDistanceThreshold)
            return false;

        currentDistance =
            Mathf.Clamp(
                routeState.DistanceTravelled,
                0f,
                totalPathLength);

        if (currentDistance >= totalPathLength - ArrivalDistanceThreshold)
            return false;

        float routeStepDistance =
            GetNpcRoutePlanStepDistance(
                Mathf.Max(0f, npc.Speed));

        float lockedPrefixLength =
            Mathf.Max(
                ArrivalDistanceThreshold,
                routeStepDistance * lockedPrefixSlots);

        lockedPrefixDistance =
            Mathf.Clamp(
                currentDistance + lockedPrefixLength,
                0f,
                totalPathLength);

        if (lockedPrefixDistance <= currentDistance + ArrivalDistanceThreshold)
            return false;

        lockedPrefixEndPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                lockedPrefixDistance);

        lockedPrefixEndFacingDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeState.Path,
                    lockedPrefixDistance)
                : GetNpcDirectionOnPathAtDistance(
                    routeState.Path,
                    lockedPrefixDistance);

        if (lockedPrefixEndFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            lockedPrefixEndFacingDirection = GetNpcSafeFacingDirection(npc);

        if (lockedPrefixEndFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        lockedPrefixEndFacingDirection.Normalize();
        return true;
    }

    private bool TryContinueCurrentNpcRouteAfterFailedRefresh(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        Vector3 finalTargetPosition,
        int currentTick)
    {
        if (!ShouldUseNpcRouteLockedPrefix(npc) ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1 ||
            !IsSameNpcMovementRouteContext(routeState, npc))
        {
            return false;
        }

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                finalTargetPosition);

        if (routeState.DistanceTravelled >= totalRouteLength - arrivalThreshold)
            return false;

        routeState.Tick = currentTick;

        float distancePerTick =
            Mathf.Max(0f, npc.Speed) *
            GetSpeedMultiplier();

        float nextPreviewDistance =
            Mathf.Clamp(
                routeState.DistanceTravelled + distancePerTick,
                0f,
                totalRouteLength);

        Vector3 movementTargetPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                nextPreviewDistance);

        npc.CurrentMovementTargetPosition = movementTargetPosition;
        npc.TickMovementTargetPosition = movementTargetPosition;
        npc.TickMovementDirectionTick = currentTick;
        npc.TickMovementArrived = false;

        if (ShouldLogNpcRouteDecision())
        {
            LogNpcMovementDebug(
                "[NPC-ROUTE-REFRESH-FAILED-KEEP-OLD]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | DistanceTravelled=" + routeState.DistanceTravelled.ToString("0.###") +
                " | TotalRouteLength=" + totalRouteLength.ToString("0.###") +
                " | OldDestination=" + FormatVector3(routeState.Destination) +
                " | NewDestination=" + FormatVector3(finalTargetPosition));
        }

        return true;
    }

    private bool ShouldUseNpcRouteLockedPrefix(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (ShouldNeverRefreshNpcFixedRouteUntilArrival(npc))
            return false;

        if (!ShouldKeepNpcRouteUntilArrival(npc))
            return false;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return true;

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem &&
            npc.CurrentBehavior == SystemNpcBehaviorType.PlanetToPlanetTravel)
        {
            return true;
        }

        return false;
    }

    private int GetNpcRouteLockedPrefixSlots()
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return movementConfig != null
            ? movementConfig.MovingDestinationRouteRefreshBlockedInitialSlots
            : 2;
    }

    private void BuildNpcRoutePrefix(
        IReadOnlyList<Vector3> sourcePath,
        float prefixDistance,
        List<Vector3> destinationPath)
    {
        if (destinationPath == null)
            return;

        destinationPath.Clear();

        if (sourcePath == null ||
            sourcePath.Count == 0)
        {
            return;
        }

        destinationPath.Add(sourcePath[0]);

        if (sourcePath.Count == 1)
            return;

        float remainingDistance =
            Mathf.Max(0f, prefixDistance);

        for (int i = 1; i < sourcePath.Count; i++)
        {
            Vector3 from = sourcePath[i - 1];
            Vector3 to = sourcePath[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= 0.001f)
                continue;

            if (remainingDistance >= segmentDistance)
            {
                AddNpcRoutePointIfDifferent(
                    destinationPath,
                    to);

                remainingDistance -= segmentDistance;
                continue;
            }

            float t =
                Mathf.Clamp01(
                    remainingDistance / segmentDistance);

            Vector3 prefixEnd =
                Vector3.Lerp(
                    from,
                    to,
                    t);

            AddNpcRoutePointIfDifferent(
                destinationPath,
                prefixEnd);

            return;
        }
    }

    private void AppendNpcRouteTail(
        IReadOnlyList<Vector3> tailPath,
        List<Vector3> destinationPath)
    {
        if (tailPath == null ||
            destinationPath == null ||
            tailPath.Count == 0)
        {
            return;
        }

        int startIndex =
            destinationPath.Count > 0
                ? 1
                : 0;

        for (int i = startIndex; i < tailPath.Count; i++)
        {
            AddNpcRoutePointIfDifferent(
                destinationPath,
                tailPath[i]);
        }
    }

    private void AddNpcRoutePointIfDifferent(
        List<Vector3> path,
        Vector3 point)
    {
        if (path == null)
            return;

        if (path.Count > 0 &&
            Vector3.Distance(
                path[path.Count - 1],
                point) <= 0.001f)
        {
            return;
        }

        path.Add(point);
    }

    private bool ShouldRejectNpcRawFallbackRoute(
        SystemNpcRuntimeState npc,
        Vector2 startFacingDirection,
        SystemShipRouteResult2A routeResult,
        out string rawFallbackDiagnosisLogFields)
    {
        rawFallbackDiagnosisLogFields = string.Empty;

        if (npc == null ||
            routeResult == null ||
            routeResult.Path == null ||
            routeResult.Path.Count <= 1 ||
            !routeResult.UsedRawSafeFallback)
        {
            return false;
        }

        Vector2 routeStartDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f)
                : GetNpcDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f);

        if (startFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude ||
            routeStartDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return false;
        }

        float startTurnAngle =
            Mathf.Abs(
                Vector2.SignedAngle(
                    startFacingDirection.normalized,
                    routeStartDirection.normalized));

        float maxAllowedStartTurnAngle =
            GetNpcRawFallbackMaxStartTurnAngleDegrees(npc);

        rawFallbackDiagnosisLogFields =
            BuildNpcRawFallbackDiagnosisLogFields(
                npc,
                startFacingDirection,
                routeStartDirection,
                startTurnAngle,
                maxAllowedStartTurnAngle,
                routeResult);

        return startTurnAngle > maxAllowedStartTurnAngle;
    }

    private bool ShouldUsePlayerLikeFixedPointRoutePipeline(
        SystemNpcRuntimeState npc,
        string routeTargetKind)
    {
        if (npc == null)
            return false;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return false;

        if (npc.IsEnemy)
            return false;

        if (!string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
            return false;

        return routeTargetKind == "FIXED_PATROL_POINT" ||
               routeTargetKind == "FIXED_MAP_POINT" ||
               routeTargetKind == "FIXED_SYSTEM_EXIT_POINT";
    }

    private string BuildNpcRawFallbackDiagnosisLogFields(
        SystemNpcRuntimeState npc,
        Vector2 startFacingDirection,
        Vector2 routeStartDirection,
        float startTurnAngle,
        float maxAllowedStartTurnAngle,
        SystemShipRouteResult2A routeResult)
    {
        IReadOnlyList<Vector3> path = routeResult.Path;

        Vector3 pathStart =
            path != null && path.Count > 0
                ? path[0]
                : Vector3.zero;

        Vector3 pathSecond =
            path != null && path.Count > 1
                ? path[1]
                : Vector3.zero;

        Vector3 pathEnd =
            path != null && path.Count > 0
                ? path[path.Count - 1]
                : Vector3.zero;

        float directDistance =
            Vector3.Distance(
                pathStart,
                pathEnd);

        float firstSegmentLength =
            path != null && path.Count > 1
                ? Vector3.Distance(path[0], path[1])
                : 0f;

        float pathLengthToDirectRatio =
            directDistance > 0.001f
                ? routeResult.PathLength / directDistance
                : 0f;

        float firstSegmentToDirectPercent =
            directDistance > 0.001f
                ? firstSegmentLength / directDistance * 100f
                : 0f;

        string rejectBucket =
            GetNpcRawFallbackRejectBucket(
                startTurnAngle,
                maxAllowedStartTurnAngle,
                routeResult,
                directDistance,
                firstSegmentLength,
                pathLengthToDirectRatio);

        return " | RawFallbackRejectReason=START_TURN_ANGLE" +
               " | RawFallbackRejectBucket=" + rejectBucket +
               " | RawFallbackStartTurnAngle=" + startTurnAngle.ToString("0.###") +
               " | RawFallbackMaxAllowedStartTurnAngle=" + maxAllowedStartTurnAngle.ToString("0.###") +
               " | RawFallbackAngleOverLimit=" + (startTurnAngle - maxAllowedStartTurnAngle).ToString("0.###") +
               " | RawFallbackStartFacing=" + FormatVector2(startFacingDirection) +
               " | RawFallbackRouteStartDirection=" + FormatVector2(routeStartDirection) +
               " | RawFallbackPathCount=" + (path != null ? path.Count : 0) +
               " | RawFallbackPathLength=" + routeResult.PathLength.ToString("0.###") +
               " | RawFallbackDirectDistance=" + directDistance.ToString("0.###") +
               " | RawFallbackPathLengthToDirectRatio=" + pathLengthToDirectRatio.ToString("0.###") +
               " | RawFallbackFirstSegmentLength=" + firstSegmentLength.ToString("0.###") +
               " | RawFallbackFirstSegmentToDirectPercent=" + firstSegmentToDirectPercent.ToString("0.###") +
               " | RawFallbackUsedSunAvoidance=" + routeResult.UsedSunAvoidance +
               " | RawFallbackDestinationCase=" + routeResult.DestinationCase +
               " | RawFallbackStartPushedOutsideSun=" + routeResult.StartWasPushedOutsideSun +
               " | RawFallbackDestinationPushedOutsideSun=" + routeResult.DestinationWasPushedOutsideSun +
               " | RawFallbackPathStart=" + FormatVector3(pathStart) +
               " | RawFallbackPathSecond=" + FormatVector3(pathSecond) +
               " | RawFallbackPathEnd=" + FormatVector3(pathEnd) +
               BuildNpcRawFallbackSunDiagnosisLogFields(npc, path);
    }

    private string GetNpcRawFallbackRejectBucket(
        float startTurnAngle,
        float maxAllowedStartTurnAngle,
        SystemShipRouteResult2A routeResult,
        float directDistance,
        float firstSegmentLength,
        float pathLengthToDirectRatio)
    {
        if (routeResult == null)
            return "UNKNOWN";

        float angleOverLimit =
            startTurnAngle - maxAllowedStartTurnAngle;

        if (routeResult.UsedSunAvoidance &&
            angleOverLimit >= 90f)
        {
            return "SUN_AVOIDANCE_EXTREME_START_TURN";
        }

        if (routeResult.UsedSunAvoidance)
            return "SUN_AVOIDANCE_START_TURN";

        if (routeResult.Path != null &&
            routeResult.Path.Count <= 2 &&
            angleOverLimit >= 90f)
        {
            return "DIRECT_ROUTE_EXTREME_START_TURN";
        }

        if (routeResult.Path != null &&
            routeResult.Path.Count <= 2)
        {
            return "DIRECT_ROUTE_START_TURN";
        }

        if (directDistance > 0.001f &&
            firstSegmentLength / directDistance <= 0.05f)
        {
            return "TINY_FIRST_SEGMENT_START_TURN";
        }

        if (pathLengthToDirectRatio >= 2f)
            return "LONG_RAW_ROUTE_START_TURN";

        return "RAW_ROUTE_START_TURN";
    }

    private string BuildNpcRawFallbackSunDiagnosisLogFields(
        SystemNpcRuntimeState npc,
        IReadOnlyList<Vector3> path)
    {
        if (npc == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            return " | RawFallbackHasSun=False";
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null ||
            starSystem.Sun == null)
        {
            return " | RawFallbackHasSun=False";
        }

        Vector3 sunCenter =
            new Vector3(
                starSystem.Sun.LocalOffset.x,
                starSystem.Sun.LocalOffset.y,
                -2f);

        float sunWorldRadius =
            Mathf.Max(
                0f,
                GetSunWorldSize(starSystem.Sun) * 0.5f);

        float sunBlockingRadius =
            sunWorldRadius + SunAvoidanceSafetyMargin;

        Vector3 pathStart =
            path != null && path.Count > 0
                ? path[0]
                : Vector3.zero;

        Vector3 pathEnd =
            path != null && path.Count > 0
                ? path[path.Count - 1]
                : Vector3.zero;

        float startDistanceToSun =
            Vector3.Distance(
                pathStart,
                sunCenter);

        float destinationDistanceToSun =
            Vector3.Distance(
                pathEnd,
                sunCenter);

        float minPathDistanceToSun =
            GetMinPathDistanceToPoint(
                path,
                sunCenter);

        return " | RawFallbackHasSun=True" +
               " | RawFallbackSunCenter=" + FormatVector3(sunCenter) +
               " | RawFallbackSunWorldRadius=" + sunWorldRadius.ToString("0.###") +
               " | RawFallbackSunBlockingRadius=" + sunBlockingRadius.ToString("0.###") +
               " | RawFallbackStartDistanceToSun=" + startDistanceToSun.ToString("0.###") +
               " | RawFallbackDestinationDistanceToSun=" + destinationDistanceToSun.ToString("0.###") +
               " | RawFallbackMinPathDistanceToSun=" + minPathDistanceToSun.ToString("0.###") +
               " | RawFallbackStartInsideSunBlock=" + (startDistanceToSun <= sunBlockingRadius) +
               " | RawFallbackDestinationInsideSunBlock=" + (destinationDistanceToSun <= sunBlockingRadius) +
               " | RawFallbackPathTouchesSunBlock=" + (minPathDistanceToSun <= sunBlockingRadius);
    }

    private float GetMinPathDistanceToPoint(
        IReadOnlyList<Vector3> path,
        Vector3 point)
    {
        if (path == null ||
            path.Count == 0)
        {
            return 0f;
        }

        float minDistance = float.MaxValue;

        for (int i = 0; i < path.Count; i++)
        {
            float distance =
                Vector3.Distance(
                    path[i],
                    point);

            if (distance < minDistance)
                minDistance = distance;
        }

        return minDistance == float.MaxValue
            ? 0f
            : minDistance;
    }

    private float GetNpcRawFallbackMaxStartTurnAngleDegrees(SystemNpcRuntimeState npc)
    {
        return Mathf.Clamp(
            GetMovementTurnSpikeAngleDegrees(),
            1f,
            179f);
    }

    private SystemShipRouteSettings2A CreateNpcRouteSettings(
        SystemNpcRuntimeState npc,
        float arrivalThreshold,
        bool debugMilitary)
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        System.Action<string> routeDebugLog =
            ShouldLogShipRouteInternals()
                ? Bootstrapper.Instance.CreateDebugLogAction(DebugLogChannel.ShipRoute)
                : null;

        float minTurnRadiusAbsolute =
            movementConfig != null
                ? movementConfig.MinRouteTurnRadiusAbsolute
                : 30f;

        float turnRadius =
            Mathf.Max(0f, npc != null ? npc.TurnRadius : 0f);

        if (minTurnRadiusAbsolute > 0f)
        {
            turnRadius =
                Mathf.Max(
                    turnRadius,
                    minTurnRadiusAbsolute);
        }

        bool isFinalPlanetApproach =
            IsNpcFinalPlanetApproachRoutePriorityCandidate(npc);

        return new SystemShipRouteSettings2A
        {
            Speed = Mathf.Max(0.01f, npc != null ? npc.Speed : 0f),
            TurnRadius = turnRadius,
            ArrivalDistanceThreshold = arrivalThreshold,
            SunAvoidanceSafetyMargin = SunAvoidanceSafetyMargin,
            SunAvoidanceArcSegments = SunAvoidanceArcSegments,
            AllowSunAvoidance = true,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = minTurnRadiusAbsolute,
            BehindSmallTurnAngleToleranceDegrees = movementConfig != null ? movementConfig.RouteBehindSmallTurnAngleToleranceDegrees : 75f,
            MaxRoutePlanSteps = NpcRoutePlanMaxSteps,
            SunAvoidanceTurnRouteReserveMultiplier = 1.5f,

            RouteBuildTimeBudgetMs =
                isFinalPlanetApproach
                    ? 0f
                    : GetRouteBuildTimeBudgetMsPerTick(),

            GetRouteBuildBudgetUsedMs =
                isFinalPlanetApproach
                    ? null
                    : GetRouteBuildBudgetUsedMs,

            AddRouteBuildBudgetUsedMs =
                isFinalPlanetApproach
                    ? null
                    : ConsumeRouteBuildBudgetFromShipRouteService,

            DebugLog = routeDebugLog,
            DebugPrefix = "[NpcMovement] "
        };
    }

    private double GetRouteBuildBudgetUsedMs()
    {
        return _routeBuildBudgetUsedMs;
    }

    private void ConsumeRouteBuildBudgetFromShipRouteService(double elapsedMs)
    {
        if (elapsedMs <= 0d)
            return;

        _routeBuildBudgetUsedMs += elapsedMs;
    }

    private bool ShouldLogShipRouteInternals()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.IncludeShipRouteInternalLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.ShipRoute);
    }

    private bool ShouldLogNpcRouteDecision()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.NpcRouteDecisionLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.NpcMovement);
    }

    private bool IsNpcMovementDebugEnabled()
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.NpcMovement);
    }

    private bool IsNpcTurnSpikeDebugEnabled()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.NpcTurnSpikeLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.NpcMovement);
    }

    private float GetMovementTurnSpikeAngleDegrees()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null
            ? debugLogConfig.MovementTurnSpikeAngleDegrees
            : 120f;
    }

    private void LogNpcMovementDebug(string message)
    {
        if (Bootstrapper.Instance == null)
            return;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.NpcMovement,
            "[SystemNpcMovementService] " + message);
    }

    private float GetSignedAngle(Vector3 from, Vector3 to)
    {
        Vector2 from2 =
            new Vector2(from.x, from.y);

        Vector2 to2 =
            new Vector2(to.x, to.y);

        if (from2.sqrMagnitude <= DirectionThresholdSqrMagnitude ||
            to2.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return 0f;
        }

        return Vector2.SignedAngle(from2.normalized, to2.normalized);
    }

    private string FormatVector3(Vector3 value)
    {
        return "(" +
               value.x.ToString("0.###") + ", " +
               value.y.ToString("0.###") + ", " +
               value.z.ToString("0.###") + ")";
    }

    private string FormatVector2(Vector2 value)
    {
        return "(" +
               value.x.ToString("0.###") + ", " +
               value.y.ToString("0.###") + ")";
    }

    private float GetNpcRouteArrivalDistanceThreshold(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition)
    {
        if (npc == null)
            return ArrivalDistanceThreshold;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
        {
            return Mathf.Max(
                ArrivalDistanceThreshold,
                GetNpcCombatRouteArrivalDistanceThreshold(npc));
        }

        if (npc.CurrentBehavior != SystemNpcBehaviorType.PlanetToPlanetTravel &&
            npc.TravelState != SystemNpcTravelState.TravelingInsideSystem)
        {
            return ArrivalDistanceThreshold;
        }

        if (string.IsNullOrWhiteSpace(npc.TargetPlanetId))
            return ArrivalDistanceThreshold;

        PlanetConfig planet =
            _configService != null
                ? _configService.GetPlanetConfigById(npc.TargetPlanetId)
                : null;

        float planetRadius =
            GetPlanetWorldSize(planet) * 0.5f;

        return Mathf.Max(
            ArrivalDistanceThreshold,
            planetRadius);
    }

    private float GetNpcCombatRouteArrivalDistanceThreshold(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return ArrivalDistanceThreshold;

        float turnRadiusPart =
            Mathf.Max(0f, npc.TurnRadius) * 0.05f;

        return Mathf.Clamp(
            turnRadiusPart,
            ArrivalDistanceThreshold,
            25f);
    }

    private float GetPlanetWorldSize(PlanetConfig planet)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService
                .SystemVisualConfig
                .GetPlanetWorldSize(planet);
        }

        return planet != null
            ? planet.VisualSize
            : 0f;
    }

    private void BuildNpcTravelWaypoints(
    SystemNpcRuntimeState npc,
    Vector3 destinationPosition,
    List<Vector3> waypoints)
    {
        waypoints.Clear();

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null ||
            starSystem.Sun == null)
        {
            waypoints.Add(npc.CurrentPosition);
            waypoints.Add(destinationPosition);

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-ROUTE-BUILD] Waypoints without sun avoidance. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", HasStarSystem=" + (starSystem != null) +
                    ", HasSun=" + (starSystem != null && starSystem.Sun != null) +
                    ", Waypoints=" + FormatNpcRoutePathForDebug(waypoints));
            }

            return;
        }

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                npc.CurrentPosition.z);

        float sunRadius =
            Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);

        float avoidanceRadius =
            sunRadius + SunAvoidanceSafetyMargin;

        Vector2 safeFacingDirection =
            GetNpcSafeFacingDirection(npc);

        float safeTurnRadius =
            Mathf.Max(0f, npc.TurnRadius);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Sun avoidance input. " +
                "Npc=" + npc.RuntimeNpcId +
                ", StarSystem=" + starSystem.Id +
                ", From=" + npc.CurrentPosition +
                ", To=" + destinationPosition +
                ", SunCenter=" + sunCenter +
                ", SunWorldRadius=" + sunRadius +
                ", SunAvoidanceSafetyMargin=" + SunAvoidanceSafetyMargin +
                ", AvoidanceRadius=" + avoidanceRadius +
                ", ArcSegments=" + SunAvoidanceArcSegments +
                ", FacingDirection=" + safeFacingDirection +
                ", TurnRadius=" + safeTurnRadius);

            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Sun avoidance distances. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromToSunDistance=" + Vector3.Distance(npc.CurrentPosition, sunCenter) +
                ", DestinationToSunDistance=" + Vector3.Distance(destinationPosition, sunCenter) +
                ", FromToDestinationDistance=" + Vector3.Distance(npc.CurrentPosition, destinationPosition));
        }

        SystemTravelSunAvoidancePath2A.BuildPath(
            waypoints,
            npc.CurrentPosition,
            destinationPosition,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments,
            false,
            safeFacingDirection,
            safeTurnRadius);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Sun avoidance result. " +
                "Npc=" + npc.RuntimeNpcId +
                ", WaypointCount=" + waypoints.Count +
                ", WaypointPathLength=" + GetNpcPathLength(waypoints) +
                ", Waypoints=" + FormatNpcRoutePathForDebug(waypoints));
        }
    }

    private string FormatNpcRoutePathForDebug(
    IReadOnlyList<Vector3> path)
    {
        if (path == null)
            return "null";

        if (path.Count == 0)
            return "empty";

        const int maxPoints = 16;

        List<string> points =
            new List<string>();

        int visibleCount =
            Mathf.Min(path.Count, maxPoints);

        for (int i = 0; i < visibleCount; i++)
        {
            points.Add(
                i + ":" + path[i]);
        }

        if (path.Count > maxPoints)
        {
            points.Add("...");
            points.Add((path.Count - 1) + ":" + path[path.Count - 1]);
        }

        return string.Join(" | ", points);
    }

    private float GetNpcRouteLastPointDistanceToDestination(
    IReadOnlyList<Vector3> path,
    Vector3 destinationPosition)
    {
        if (path == null ||
            path.Count == 0)
        {
            return -1f;
        }

        return Vector3.Distance(
            path[path.Count - 1],
            destinationPosition);
    }

    private void AddNpcSmallRoutePreviewDots2A(
    TravelRoutePreview2A preview,
    List<Vector3> path,
    float intervalStartDistance,
    float intervalEndDistance,
    float passedDistance,
    int tickIndex,
    float smallDotSpacing,
    int maxSmallDots)
    {
        if (preview == null ||
            path == null ||
            path.Count <= 1)
        {
            return;
        }

        if (preview.SmallDotCount >= maxSmallDots)
            return;

        float safeSpacing =
            Mathf.Max(0.01f, smallDotSpacing);

        float firstDotDistance =
            Mathf.Ceil(
                (Mathf.Max(intervalStartDistance, passedDistance) + 0.001f) /
                safeSpacing) *
            safeSpacing;

        for (float dotDistance = firstDotDistance;
             dotDistance < intervalEndDistance - 0.001f;
             dotDistance += safeSpacing)
        {
            if (preview.SmallDotCount >= maxSmallDots)
                return;

            preview.AddSmallDot(
                GetNpcPointOnPathAtDistance(path, dotDistance),
                tickIndex);
        }
    }

    private bool TryGetActiveNpcPreviewRoute(
    SystemNpcRuntimeState npc,
    Vector3 destinationPosition,
    out NpcMovementRouteState routeState)
    {
        routeState = null;

        if (npc == null ||
            string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
        {
            return false;
        }

        if (!_npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out routeState))
        {
            return false;
        }

        if (routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        float totalPathLength =
            GetNpcPathLength(routeState.Path);

        if (routeState.DistanceTravelled >= totalPathLength - ArrivalDistanceThreshold)
            return false;

        return true;
    }

    private NpcMovementRouteState GetOrCreateNpcMovementRouteState(
    string runtimeNpcId)
    {
        if (!_npcMovementRoutes.TryGetValue(
                runtimeNpcId,
                out NpcMovementRouteState routeState))
        {
            routeState = new NpcMovementRouteState();
            _npcMovementRoutes[runtimeNpcId] = routeState;
        }

        return routeState;
    }

    private Vector2 GetNpcSafeFacingDirection(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return Vector2.up;

        Vector3 facing = npc.FacingDirection;
        facing.z = 0f;

        if (facing.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(facing.x, facing.y).normalized;

        Vector3 tickDirection = npc.TickMovementDirection;
        tickDirection.z = 0f;

        if (tickDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(tickDirection.x, tickDirection.y).normalized;

        Vector3 targetDirection = npc.TargetPosition - npc.CurrentPosition;
        targetDirection.z = 0f;

        if (targetDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(targetDirection.x, targetDirection.y).normalized;

        return Vector2.up;
    }

    private float GetNpcRoutePlanStepDistance(
    float speed)
    {
        int substepsPerTick =
            _configService != null &&
            _configService.ShipMovementConfig != null
                ? _configService.ShipMovementConfig.RouteSubstepsPerTick
                : 10;

        return Mathf.Max(
            ArrivalDistanceThreshold,
            speed / Mathf.Max(1, substepsPerTick));
    }

    private int GetNpcRoutePlanMaxSteps(
        float waypointPathLength,
        float turnRadius,
        float routeStepDistance)
    {
        float expectedLength =
            Mathf.Max(0f, waypointPathLength) +
            Mathf.Max(0f, turnRadius) *
            Mathf.PI *
            2f;

        int steps =
            Mathf.CeilToInt(
                expectedLength /
                Mathf.Max(ArrivalDistanceThreshold, routeStepDistance)) + 64;

        return Mathf.Clamp(
            steps,
            32,
            NpcRoutePlanMaxSteps);
    }

    private float GetNpcIntermediateWaypointArrivalDistanceThreshold(
    IReadOnlyList<Vector3> waypoints,
    float routeStepDistance)
    {
        if (waypoints == null ||
            waypoints.Count <= 2)
        {
            return ArrivalDistanceThreshold;
        }

        return Mathf.Max(
            ArrivalDistanceThreshold,
            routeStepDistance * 2f);
    }

    private float GetNpcRouteStraightExitAngleDegrees()
    {
        if (_configService == null ||
            _configService.ShipMovementConfig == null)
        {
            return 3f;
        }

        return _configService.ShipMovementConfig.RouteStraightExitAngleDegrees;
    }

    private float GetNpcPathLength(
        IReadOnlyList<Vector3> path)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        float length = 0f;

        for (int i = 1; i < path.Count; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        return length;
    }

    private Vector3 GetNpcPointOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count == 0)
        {
            return Vector3.zero;
        }

        if (path.Count == 1)
            return path[0];

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                float t = remainingDistance / segmentDistance;
                return Vector3.Lerp(from, to, t);
            }

            remainingDistance -= segmentDistance;
        }

        return path[path.Count - 1];
    }

    private Vector2 GetNpcDirectionOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return Vector2.up;
        }

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                Vector2 direction =
                    new Vector2(
                        to.x - from.x,
                        to.y - from.y);

                return direction.sqrMagnitude > DirectionThresholdSqrMagnitude
                    ? direction.normalized
                    : Vector2.up;
            }

            remainingDistance -= segmentDistance;
        }

        Vector3 previous = path[path.Count - 2];
        Vector3 last = path[path.Count - 1];

        Vector2 fallback =
            new Vector2(
                last.x - previous.x,
                last.y - previous.y);

        return fallback.sqrMagnitude > DirectionThresholdSqrMagnitude
            ? fallback.normalized
            : Vector2.up;
    }



    private float CalculateProgress01(
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector3 currentPosition)
    {
        float totalDistance =
            Vector3.Distance(
                startPosition,
                destinationPosition);

        if (totalDistance <= ArrivalDistanceThreshold)
            return 1f;

        float remainingDistance =
            Vector3.Distance(
                currentPosition,
                destinationPosition);

        return Mathf.Clamp01(
            1f - remainingDistance / totalDistance);
    }

    private Vector3 GetSunSafeNextTargetPosition(
        SystemNpcRuntimeState npc,
        Vector3 finalTargetPosition)
    {
        if (npc == null)
            return finalTargetPosition;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null || starSystem.Sun == null)
        {
            ClearSunAvoidanceRoute(npc.RuntimeNpcId);
            return finalTargetPosition;
        }

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            npc.CurrentPosition.z);

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float avoidanceRadius = sunRadius + SunAvoidanceSafetyMargin;

        SunAvoidanceRouteState routeState = GetOrCreateSunAvoidanceRoute(
            npc,
            finalTargetPosition,
            sunCenter,
            avoidanceRadius);

        if (routeState == null)
            return finalTargetPosition;

        while (routeState.WaypointIndex < routeState.Waypoints.Count - 1 &&
               Vector3.Distance(
                   npc.CurrentPosition,
                   routeState.Waypoints[routeState.WaypointIndex]) <= ArrivalDistanceThreshold)
        {
            routeState.WaypointIndex++;
        }

        if (routeState.WaypointIndex >= routeState.Waypoints.Count - 1)
            return finalTargetPosition;

        return routeState.Waypoints[routeState.WaypointIndex];
    }

    private float GetSunWorldSize(SunConfig sun)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService
                .SystemVisualConfig
                .GetSunWorldSize(sun);
        }

        return sun != null
            ? sun.VisualSize
            : 0f;
    }

    private SunAvoidanceRouteState GetOrCreateSunAvoidanceRoute(
        SystemNpcRuntimeState npc,
        Vector3 finalTargetPosition,
        Vector3 sunCenter,
        float avoidanceRadius)
    {
        if (string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
            return null;

        bool shouldBuildRoute =
            !_sunAvoidanceRoutes.TryGetValue(npc.RuntimeNpcId, out SunAvoidanceRouteState routeState) ||
            routeState.Waypoints.Count < 2 ||
            routeState.WaypointIndex >= routeState.Waypoints.Count ||
            HasSunAvoidanceRouteTargetChanged(routeState, npc) ||
            ShouldRefreshSunAvoidanceFinalLeg(routeState, finalTargetPosition);

        if (!shouldBuildRoute)
            return routeState;

        SystemTravelSunAvoidancePath2A.BuildPath(
            _sunAvoidancePath,
            npc.CurrentPosition,
            finalTargetPosition,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments);

        if (_sunAvoidancePath.Count <= 2)
        {
            ClearSunAvoidanceRoute(npc.RuntimeNpcId);
            return null;
        }

        if (routeState == null)
            routeState = new SunAvoidanceRouteState();

        routeState.Waypoints.Clear();
        routeState.Waypoints.AddRange(_sunAvoidancePath);
        routeState.Destination = finalTargetPosition;
        routeState.WaypointIndex = 1;
        routeState.TravelState = npc.TravelState;
        routeState.TargetSystemId = npc.TargetSystemId;
        routeState.TargetPlanetId = npc.TargetPlanetId;
        routeState.CurrentTargetRuntimeNpcId = npc.CurrentTargetRuntimeNpcId;

        _sunAvoidanceRoutes[npc.RuntimeNpcId] = routeState;

        return routeState;
    }

    private bool HasSunAvoidanceRouteTargetChanged(
        SunAvoidanceRouteState routeState,
        SystemNpcRuntimeState npc)
    {
        if (routeState == null || npc == null)
            return true;

        return routeState.TravelState != npc.TravelState ||
               routeState.TargetSystemId != npc.TargetSystemId ||
               routeState.TargetPlanetId != npc.TargetPlanetId ||
               routeState.CurrentTargetRuntimeNpcId != npc.CurrentTargetRuntimeNpcId;
    }

    private bool ShouldRefreshSunAvoidanceFinalLeg(
        SunAvoidanceRouteState routeState,
        Vector3 finalTargetPosition)
    {
        if (routeState == null)
            return true;

        if (routeState.WaypointIndex < routeState.Waypoints.Count - 1)
            return false;

        return Vector3.Distance(routeState.Destination, finalTargetPosition) >
               SunAvoidanceDestinationRefreshThreshold;
    }

    private void AdvanceSunAvoidanceRoute(SystemNpcRuntimeState npc)
    {
        if (npc == null || string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
            return;

        if (!_sunAvoidanceRoutes.TryGetValue(npc.RuntimeNpcId, out SunAvoidanceRouteState routeState))
            return;

        if (routeState.WaypointIndex < routeState.Waypoints.Count - 1)
            routeState.WaypointIndex++;
    }

    private void ClearSunAvoidanceRoute(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        _sunAvoidanceRoutes.Remove(runtimeNpcId);
    }

    private void CompleteMovement(SystemNpcRuntimeState npc, int currentTick)
    {
        CompleteMovement(npc, currentTick, null);
    }

    private void CompleteMovement(
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long completeStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        if (npc == null)
            return;

        long phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        SystemNpcBehaviorType completedBehavior =
            npc.CurrentBehavior;

        SystemNpcTravelState completedTravelState =
            npc.TravelState;

        Vector3 completedFromPosition =
            npc.CurrentPosition;

        Vector3 completedTargetPosition =
            npc.TargetPosition;

        string completedScope =
            GetPerfScope(npc.CurrentSystemId);

        string completedCurrentSystemId =
            GetCurrentSystemIdForPerf();

        string completedNpcSystemId =
            npc.CurrentSystemId;

        string completedRouteTargetKind =
            GetNpcRouteTargetKind(npc);

        string completedRouteTargetMoveKind =
            GetNpcRouteTargetMoveKind(npc);

        string completedRouteTargetId =
            GetNpcRouteTargetId(npc);

        string completedTargetPlanetId =
            npc.TargetPlanetId;

        string completedTargetSystemId =
            npc.TargetSystemId;

        string completedTargetNpcId =
            npc.CurrentTargetRuntimeNpcId;

        string completedCurrentPlanetBefore =
            npc.CurrentPlanetId;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                npc.TargetPosition);

        float completedDistanceToTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                npc.TargetPosition);

        if (phaseStats != null)
        {
            phaseStats.CompletePrecheckMs += EndPerfMeasureMs(phaseStartedAt);
            phaseStats.CompleteRouteTargetKind = completedRouteTargetKind;
            phaseStats.CompleteRouteTargetMoveKind = completedRouteTargetMoveKind;
            phaseStats.CompleteBehaviorBefore = completedBehavior.ToString();
            phaseStats.CompleteTravelStateBefore = completedTravelState.ToString();
            phaseStats.CompleteSystemBefore = completedNpcSystemId ?? string.Empty;
            phaseStats.CompleteTargetSystemId = completedTargetSystemId ?? string.Empty;
            phaseStats.CompleteTargetPlanetId = completedTargetPlanetId ?? string.Empty;
            phaseStats.CompleteCurrentPlanetBefore = completedCurrentPlanetBefore ?? string.Empty;
            phaseStats.CompleteWasSystemTravel =
                completedTravelState == SystemNpcTravelState.TravelingToAnotherSystem;
        }

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteMovement start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CompletedBehavior=" + completedBehavior +
                ", PrevBehaviorBeforeComplete=" + npc.PrevBehavior +
                ", CompletedTravelState=" + completedTravelState +
                ", IsOnPlanetBefore=" + npc.IsOnPlanet +
                ", CurrentPlanetBefore=" + npc.CurrentPlanetId +
                ", TargetPlanetBefore=" + npc.TargetPlanetId +
                ", CurrentPositionBefore=" + npc.CurrentPosition +
                ", TargetPositionBefore=" + npc.TargetPosition +
                ", DistanceToTargetBefore=" + completedDistanceToTarget +
                ", ArrivalThresholdUsed=" + arrivalThreshold +
                ", Tick=" + currentTick);
        }

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem &&
            completedDistanceToTarget > arrivalThreshold)
        {
            if (phaseStats != null)
                phaseStats.CompleteWasBlocked = true;

            LogNpcPlanetApproachQueueCoordinates(
                "COMPLETE_BLOCKED_TARGET_STILL_FAR",
                npc,
                currentTick,
                null);

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-MOVEMENT] CompleteMovement blocked: target planet is still far. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", CurrentPosition=" + npc.CurrentPosition +
                    ", TargetPosition=" + npc.TargetPosition +
                    ", DistanceToTarget=" + completedDistanceToTarget +
                    ", ArrivalThresholdUsed=" + arrivalThreshold +
                    ", TargetPlanet=" + npc.TargetPlanetId +
                    ", Tick=" + currentTick);
            }

            if (IsNpcMovementPerformanceLogEnabled())
            {
                phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

                Bootstrapper.Instance.LogPerformance(
                    DebugLogPerformanceArea.NpcMovement,
                    "[SystemNpcMovementService] NPC_MOVEMENT_COMPLETION_BLOCKED" +
                    " | Scope=" + completedScope +
                    " | CurrentSystemId=" + completedCurrentSystemId +
                    " | NpcSystemId=" + completedNpcSystemId +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Behavior=" + completedBehavior +
                    " | TravelStateBefore=" + completedTravelState +
                    " | TravelStateAfter=" + npc.TravelState +
                    " | RouteTargetKind=" + completedRouteTargetKind +
                    " | RouteTargetMoveKind=" + completedRouteTargetMoveKind +
                    " | RouteTargetId=" + completedRouteTargetId +
                    " | TargetSystemId=" + completedTargetSystemId +
                    " | TargetPlanetId=" + completedTargetPlanetId +
                    " | TargetNpcId=" + completedTargetNpcId +
                    " | FromPosition=" + FormatVector3(completedFromPosition) +
                    " | TargetPosition=" + FormatVector3(completedTargetPosition) +
                    " | DistanceToTarget=" + completedDistanceToTarget.ToString("0.###") +
                    " | ArrivalThreshold=" + arrivalThreshold.ToString("0.###") +
                    " | Tick=" + currentTick +
                    " | Ms=0.00");

                if (phaseStats != null)
                    phaseStats.CompleteLogMs += EndPerfMeasureMs(phaseStartedAt);
            }

            phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            ClearNpcMovementRoute(npc.RuntimeNpcId);
            npc.TravelProgress01 = 0f;

            if (phaseStats != null)
            {
                phaseStats.CompleteClearRouteMs += EndPerfMeasureMs(phaseStartedAt);
                phaseStats.CompleteTravelStateAfter = npc.TravelState.ToString();
                phaseStats.CompleteSystemAfter = npc.CurrentSystemId ?? string.Empty;
                phaseStats.CompleteCurrentPlanetAfter = npc.CurrentPlanetId ?? string.Empty;
                phaseStats.CompleteTotalMs += EndPerfMeasureMs(completeStartedAt);
            }

            return;
        }

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        ClearNpcMovementRoute(npc.RuntimeNpcId);

        if (phaseStats != null)
            phaseStats.CompleteClearRouteMs += EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        npc.CurrentPosition = npc.TargetPosition;
        npc.TravelProgress01 = 1f;

        bool completedSystemTravel =
            npc.TravelState == SystemNpcTravelState.TravelingToAnotherSystem;

        if (phaseStats != null)
            phaseStats.CompleteAssignPositionMs += EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        switch (npc.TravelState)
        {
            case SystemNpcTravelState.TravelingInsideSystem:
                npc.IsOnPlanet = true;
                npc.TravelState = SystemNpcTravelState.OnPlanet;
                npc.CurrentPlanetId = npc.TargetPlanetId;
                break;

            case SystemNpcTravelState.TravelingToAnotherSystem:
                CompleteSystemTravel(npc);
                break;

            case SystemNpcTravelState.Patrolling:
                npc.TravelState = SystemNpcTravelState.Idle;
                break;

            case SystemNpcTravelState.EngagingEnemy:
                npc.TravelState = SystemNpcTravelState.Idle;
                break;
        }

        if (phaseStats != null)
        {
            double stateSwitchMs = EndPerfMeasureMs(phaseStartedAt);
            phaseStats.CompleteStateSwitchMs += stateSwitchMs;

            if (completedSystemTravel)
                phaseStats.CompleteSystemTravelMs += stateSwitchMs;
        }

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        npc.StartPosition = npc.CurrentPosition;

        if (!completedSystemTravel)
            npc.TargetPosition = Vector3.zero;

        if (phaseStats != null)
            phaseStats.CompleteAssignPositionMs += EndPerfMeasureMs(phaseStartedAt);

        if (IsNpcMovementPerformanceLogEnabled())
        {
            phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

            Bootstrapper.Instance.LogPerformance(
                DebugLogPerformanceArea.NpcMovement,
                "[SystemNpcMovementService] NPC_MOVEMENT_COMPLETED" +
                " | Scope=" + completedScope +
                " | CurrentSystemId=" + completedCurrentSystemId +
                " | NpcSystemId=" + completedNpcSystemId +
                " | Npc=" + npc.RuntimeNpcId +
                " | Behavior=" + completedBehavior +
                " | TravelStateBefore=" + completedTravelState +
                " | TravelStateAfter=" + npc.TravelState +
                " | RouteTargetKind=" + completedRouteTargetKind +
                " | RouteTargetMoveKind=" + completedRouteTargetMoveKind +
                " | RouteTargetId=" + completedRouteTargetId +
                " | TargetSystemId=" + completedTargetSystemId +
                " | TargetPlanetId=" + completedTargetPlanetId +
                " | TargetNpcId=" + completedTargetNpcId +
                " | IsOnPlanetAfter=" + npc.IsOnPlanet +
                " | CurrentPlanetAfter=" + npc.CurrentPlanetId +
                " | FromPosition=" + FormatVector3(completedFromPosition) +
                " | CompletedTargetPosition=" + FormatVector3(completedTargetPosition) +
                " | PositionAfter=" + FormatVector3(npc.CurrentPosition) +
                " | DistanceToTarget=" + completedDistanceToTarget.ToString("0.###") +
                " | ArrivalThreshold=" + arrivalThreshold.ToString("0.###") +
                " | Tick=" + currentTick +
                " | Ms=0.00");

            if (phaseStats != null)
                phaseStats.CompleteLogMs += EndPerfMeasureMs(phaseStartedAt);
        }

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteMovement state BEFORE behavior complete. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CompletedBehavior=" + completedBehavior +
                ", CompletedTravelState=" + completedTravelState +
                ", FromPosition=" + completedFromPosition +
                ", CompletedTargetPosition=" + completedTargetPosition +
                ", CurrentBehaviorNow=" + npc.CurrentBehavior +
                ", PrevBehaviorNow=" + npc.PrevBehavior +
                ", TravelStateNow=" + npc.TravelState +
                ", IsOnPlanetNow=" + npc.IsOnPlanet +
                ", CurrentPlanetNow=" + npc.CurrentPlanetId +
                ", TargetPlanetNow=" + npc.TargetPlanetId +
                ", CurrentPositionNow=" + npc.CurrentPosition +
                ", TargetPositionNow=" + npc.TargetPosition +
                ", Tick=" + currentTick);
        }

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        _eventBus.Publish(new SystemNpcTravelStateChangedEvent(
            npc.RuntimeNpcId,
            npc,
            npc.TravelState,
            npc.CurrentSystemId));

        if (phaseStats != null)
            phaseStats.CompletePublishEventMs += EndPerfMeasureMs(phaseStartedAt);

        phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        _behaviorService.CompleteBehavior(npc, currentTick);

        if (phaseStats != null)
            phaseStats.CompleteBehaviorMs += EndPerfMeasureMs(phaseStartedAt);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteMovement end AFTER new behavior may be assigned. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CompletedBehavior=" + completedBehavior +
                ", CompletedTravelState=" + completedTravelState +
                ", CurrentBehaviorAfter=" + npc.CurrentBehavior +
                ", PrevBehaviorAfter=" + npc.PrevBehavior +
                ", TravelStateAfter=" + npc.TravelState +
                ", IsOnPlanetAfter=" + npc.IsOnPlanet +
                ", CurrentPlanetAfter=" + npc.CurrentPlanetId +
                ", TargetPlanetAfter=" + npc.TargetPlanetId +
                ", PositionAfter=" + npc.CurrentPosition +
                ", TargetPositionAfter=" + npc.TargetPosition +
                ", Tick=" + currentTick);
        }

        if (phaseStats != null)
        {
            phaseStats.CompleteTravelStateAfter = npc.TravelState.ToString();
            phaseStats.CompleteSystemAfter = npc.CurrentSystemId ?? string.Empty;
            phaseStats.CompleteCurrentPlanetAfter = npc.CurrentPlanetId ?? string.Empty;
            phaseStats.CompleteTotalMs += EndPerfMeasureMs(completeStartedAt);
        }
    }

    private void CompleteSystemTravel(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteSystemTravel start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromSystem=" + npc.CurrentSystemId +
                ", ToSystem=" + npc.TargetSystemId +
                ", ExitPoint=" + npc.TargetSystemExitPoint +
                ", EntryPoint=" + npc.TargetSystemEntryPoint +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", Position=" + npc.CurrentPosition);
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetSystemId))
        {
            string arrivedSystemId = npc.TargetSystemId;

            npc.CurrentSystemId = arrivedSystemId;
            npc.CurrentPosition = npc.TargetSystemEntryPoint;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;

            npc.CurrentPlanetId = null;
            npc.TargetPlanetId = null;

            ApplyInitialFacingToSun(npc, arrivedSystemId);
        }

        npc.IsOnPlanet = false;
        npc.TravelState = SystemNpcTravelState.Idle;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteSystemTravel end. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CurrentSystem=" + npc.CurrentSystemId +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", TravelState=" + npc.TravelState +
                ", Position=" + npc.CurrentPosition);
        }
    }

    private void ApplyInitialFacingToSun(
    SystemNpcRuntimeState npc,
    string systemId)
    {
        if (npc == null)
            return;

        Vector3 directionToSun =
            ResolveDirectionToSun(
                systemId,
                npc.CurrentPosition);

        npc.FacingDirection = directionToSun;
        npc.TickMovementDirection = directionToSun;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = npc.CurrentPosition + directionToSun;
        npc.CurrentMovementTargetPosition = npc.TargetPosition;
        npc.TickMovementTargetPosition = npc.TargetPosition;

        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;
        npc.TravelProgress01 = 0f;
    }

    private Vector3 ResolveDirectionToSun(
    string systemId,
    Vector3 currentPosition)
    {
        if (_configService == null)
            return Vector3.up;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        if (starSystem == null || starSystem.Sun == null)
            return Vector3.up;

        Vector2 sunOffset =
            starSystem.Sun.LocalOffset;

        Vector3 sunPosition =
            new Vector3(
                sunOffset.x,
                sunOffset.y,
                currentPosition.z);

        Vector3 directionToSun =
            sunPosition - currentPosition;

        directionToSun.z = 0f;

        if (directionToSun.sqrMagnitude < 0.0001f)
            return Vector3.up;

        return directionToSun.normalized;
    }

    private sealed class SunAvoidanceRouteState
    {
        public readonly List<Vector3> Waypoints = new();
        public Vector3 Destination;
        public int WaypointIndex;
        public SystemNpcTravelState TravelState;
        public string TargetSystemId;
        public string TargetPlanetId;
        public string CurrentTargetRuntimeNpcId;
    }

    private sealed class NpcRoutePerfSnapshot
    {
        public readonly List<Vector3> Path = new();

        public string ContextHash;
        public int RouteHash;
        public float PathLength;
        public int PathCount;
        public Vector3 StartPosition;
        public Vector3 DestinationPosition;
    }

    private sealed class NpcMovementRouteState
    {
        public readonly List<Vector3> Path = new List<Vector3>();
        public readonly TravelRoutePlan RoutePlan = new TravelRoutePlan();

        public Vector3 Destination;
        public int SegmentIndex;
        public int Tick;
        public int BuildTick;
        public float DistanceTravelled;
        public float TotalDistance;
        public bool IsBoundaryEscapeRoute;
        public bool RequiresStartTurnInPlace;
        public bool StartTurnInPlacePending;
        public Vector2 StartTurnInPlaceDirection = Vector2.up;

        public SystemNpcBehaviorType BehaviorType;
        public SystemNpcTravelState TravelState;
        public string TargetSystemId;
        public string TargetPlanetId;
        public string CurrentTargetRuntimeNpcId;

        public bool IsOffscreenMovingPlanetRoute;
        public int OffscreenMovingPlanetLastRefreshTick = -1;
        public Vector3 OffscreenMovingPlanetSnapshotDestination;
        public string OffscreenMovingPlanetSnapshotTargetPlanetId = string.Empty;
    }

    private bool IsMilitaryDebugNpc(SystemNpcRuntimeState npc)
    {
        return npc != null &&
               npc.IsAlly;
        //     &&
        //    (npc.AllyRole == AllyRole2A.Military ||
        //     npc.AllyRole == AllyRole2A.Science);
    }

    private void ClearNpcMovementRoute(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        _npcMovementRoutes.Remove(runtimeNpcId);
        _npcBoundaryNavigationStates.Remove(runtimeNpcId);
        ClearSunAvoidanceRoute(runtimeNpcId);
    }

    private SystemBoundaryNavigation2A.BoundaryNavigationState GetOrCreateNpcBoundaryNavigationState(
    string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return null;

        if (!_npcBoundaryNavigationStates.TryGetValue(
                runtimeNpcId,
                out SystemBoundaryNavigation2A.BoundaryNavigationState state) ||
            state == null)
        {
            state =
                new SystemBoundaryNavigation2A.BoundaryNavigationState();

            _npcBoundaryNavigationStates[runtimeNpcId] = state;
        }

        return state;
    }

    private bool IsSameNpcMovementRouteContext(
        NpcMovementRouteState routeState,
        SystemNpcRuntimeState npc)
    {
        if (routeState == null || npc == null)
            return false;

        return routeState.BehaviorType == npc.CurrentBehavior &&
               routeState.TravelState == npc.TravelState &&
               routeState.TargetSystemId == npc.TargetSystemId &&
               routeState.TargetPlanetId == npc.TargetPlanetId &&
               routeState.CurrentTargetRuntimeNpcId == npc.CurrentTargetRuntimeNpcId;
    }

    private void SaveNpcMovementRouteContext(
        NpcMovementRouteState routeState,
        SystemNpcRuntimeState npc)
    {
        if (routeState == null || npc == null)
            return;

        routeState.BehaviorType = npc.CurrentBehavior;
        routeState.TravelState = npc.TravelState;
        routeState.TargetSystemId = npc.TargetSystemId;
        routeState.TargetPlanetId = npc.TargetPlanetId;
        routeState.CurrentTargetRuntimeNpcId = npc.CurrentTargetRuntimeNpcId;
    }

    private float GetCurrentTickRemainingFactor()
    {
        if (_gameTimeService == null &&
            Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet(
                out _gameTimeService);
        }

        if (_gameTimeService == null ||
            _gameTimeService.State == null)
        {
            return 1f;
        }

        float secondsPerTick =
            Mathf.Max(
                0.01f,
                GameTimeState.SecondsPerDay);

        float elapsedFactor =
            Mathf.Clamp01(
                _gameTimeService.State.Accumulator /
                secondsPerTick);

        return Mathf.Clamp01(
            1f - elapsedFactor);
    }

    private static float GetTickScaledDeltaTime(float deltaTime)
    {
        return deltaTime /
               Mathf.Max(0.01f, GameTimeState.SecondsPerDay);
    }

    private void RebuildNpcMovementRoutePlan(
    NpcMovementRouteState routeState,
    SystemNpcRuntimeState npc)
    {
        if (routeState == null)
            return;

        routeState.RoutePlan.Clear();
        routeState.StartTurnInPlacePending = false;
        routeState.StartTurnInPlaceDirection = Vector2.up;

        if (npc == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return;
        }

        Vector2 startFacingDirection =
            GetNpcSafeFacingDirection(npc);

        Vector2 firstRouteDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeState.Path,
                    0f)
                : GetNpcDirectionOnPathAtDistance(
                    routeState.Path,
                    0f);

        if (firstRouteDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            routeState.RoutePlan.SetMovePath(routeState.Path);
            return;
        }

        firstRouteDirection.Normalize();

        float startTurnAngle =
            Vector2.SignedAngle(
                startFacingDirection,
                firstRouteDirection);

        if (ShouldUseNpcStartTurnInPlace(
                Mathf.Abs(startTurnAngle),
                npc,
                routeState))
        {
            routeState.RoutePlan.AddTurnInPlace(
                npc.CurrentPosition,
                startFacingDirection,
                firstRouteDirection);

            routeState.StartTurnInPlacePending = true;
            routeState.StartTurnInPlaceDirection = firstRouteDirection;

            if (IsNpcMovementDebugEnabled())
            {
                LogNpcMovementDebug(
                    "[NPC-TURN-IN-PLACE-PLAN]" +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Type=" + npc.NpcType +
                    " | TurnAngle=" + startTurnAngle.ToString("0.###") +
                    " | Position=" + FormatVector3(npc.CurrentPosition) +
                    " | From=" + FormatVector2(startFacingDirection) +
                    " | To=" + FormatVector2(firstRouteDirection) +
                    " | Destination=" + FormatVector3(routeState.Destination) +
                    " | PathCount=" + routeState.Path.Count);
            }
        }

        routeState.RoutePlan.SetMovePath(routeState.Path);
    }

    private bool ShouldUseNpcStartTurnInPlace(
    float turnAngleDegrees,
    SystemNpcRuntimeState npc,
    NpcMovementRouteState routeState)
    {
        return false;
    }

    private bool IsNpcCombatOrEnemyRoute(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return true;

        if (npc.IsEnemy)
            return true;

        if (npc.IsFighting)
            return true;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return true;

        if (!string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
            return true;

        return false;
    }

    private bool ConsumeNpcStartTurnInPlaceIfNeeded(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        int currentTick)
    {
        if (npc == null ||
            routeState == null ||
            !routeState.StartTurnInPlacePending)
        {
            return false;
        }

        Vector2 turnDirection =
            routeState.StartTurnInPlaceDirection;

        if (routeState.RoutePlan.HasTurnInPlaceAtStart &&
            routeState.RoutePlan.Steps.Count > 0 &&
            routeState.RoutePlan.Steps[0].ToDirection.sqrMagnitude >
            DirectionThresholdSqrMagnitude)
        {
            turnDirection =
                routeState.RoutePlan.Steps[0].ToDirection;
        }

        if (turnDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            routeState.StartTurnInPlacePending = false;
            return false;
        }

        turnDirection.Normalize();

        Vector3 oldFacingDirection =
            npc.FacingDirection;

        Vector3 newFacingDirection =
            new Vector3(
                turnDirection.x,
                turnDirection.y,
                0f);

        npc.FacingDirection = newFacingDirection;
        npc.TickMovementDirection = newFacingDirection;

        npc.CurrentMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementDirectionTick = currentTick;
        npc.TickMovementArrived = false;

        routeState.StartTurnInPlacePending = false;
        routeState.Tick = currentTick;

        if (IsNpcMovementDebugEnabled())
        {
            LogNpcMovementDebug(
                "[NPC-TURN-IN-PLACE]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | TurnAngle=" + GetSignedAngle(
                    oldFacingDirection,
                    newFacingDirection).ToString("0.###") +
                " | Position=" + FormatVector3(npc.CurrentPosition) +
                " | OldFacing=" + FormatVector3(oldFacingDirection) +
                " | NewFacing=" + FormatVector3(newFacingDirection) +
                " | Destination=" + FormatVector3(routeState.Destination));
        }

        _eventBus.Publish(new SystemNpcPositionChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentSystemId,
            npc.CurrentPosition));

        return true;
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        if (startedAt <= 0L)
            return 0d;

        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000d / System.Diagnostics.Stopwatch.Frequency;
    }

    private void LogNpcPerf(double elapsedMs, string message)
    {
        if (!ShouldLogNpcPerf(elapsedMs))
            return;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovement,
            "[SystemNpcMovementService] " +
            message +
            " | Ms=" +
            elapsedMs.ToString("F2"));
    }

    private string GetCurrentSystemIdForPerf()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            return string.Empty;
        }

        return _gameSessionService.State.Player.CurrentSystemId ?? string.Empty;
    }

    private string GetPerfScope(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return "UNKNOWN_SYSTEM";

        string currentSystemId = GetCurrentSystemIdForPerf();

        return string.Equals(
                systemId,
                currentSystemId,
                StringComparison.Ordinal)
            ? "CURRENT_SYSTEM"
            : "OFFSCREEN_SYSTEM";
    }

    private bool IsCurrentSystemForPerf(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        return string.Equals(
            systemId,
            GetCurrentSystemIdForPerf(),
            StringComparison.Ordinal);
    }

    private string GetNpcRouteTargetKind(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "UNKNOWN";

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy ||
            !string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
        {
            return "MOVING_NPC_OR_ENEMY";
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetSystemId) &&
            npc.TargetSystemExitPoint != Vector3.zero)
        {
            return "FIXED_SYSTEM_EXIT_POINT";
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetPlanetId))
        {
            return "MOVING_PLANET";
        }

        if (npc.CurrentBehavior == SystemNpcBehaviorType.PatrolSystem ||
            npc.TravelState == SystemNpcTravelState.Patrolling)
        {
            return "FIXED_PATROL_POINT";
        }

        if (npc.TargetPosition != Vector3.zero)
        {
            return "FIXED_MAP_POINT";
        }

        return "UNKNOWN_OR_FALLBACK";
    }

    private string GetNpcRouteTargetMoveKind(SystemNpcRuntimeState npc)
    {
        string routeTargetKind = GetNpcRouteTargetKind(npc);

        if (routeTargetKind == "MOVING_PLANET" ||
            routeTargetKind == "MOVING_NPC_OR_ENEMY")
        {
            return "MOVING";
        }

        if (routeTargetKind == "FIXED_SYSTEM_EXIT_POINT" ||
            routeTargetKind == "FIXED_PATROL_POINT" ||
            routeTargetKind == "FIXED_MAP_POINT")
        {
            return "FIXED";
        }

        return "UNKNOWN";
    }

    private string GetNpcRouteTargetId(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
            return npc.CurrentTargetRuntimeNpcId;

        if (!string.IsNullOrWhiteSpace(npc.TargetSystemId))
            return npc.TargetSystemId;

        if (!string.IsNullOrWhiteSpace(npc.TargetPlanetId))
            return npc.TargetPlanetId;

        return string.Empty;
    }

    private string GetNpcRouteTargetLogFields(SystemNpcRuntimeState npc)
    {
        if (npc == null)
        {
            return " | RouteTargetKind=UNKNOWN" +
                   " | RouteTargetMoveKind=UNKNOWN" +
                   " | RouteTargetId=" +
                   " | TargetSystemId=" +
                   " | TargetPlanetId=" +
                   " | TargetNpcId=";
        }

        return " | RouteTargetKind=" + GetNpcRouteTargetKind(npc) +
               " | RouteTargetMoveKind=" + GetNpcRouteTargetMoveKind(npc) +
               " | RouteTargetId=" + GetNpcRouteTargetId(npc) +
               " | TargetSystemId=" + (npc.TargetSystemId ?? string.Empty) +
               " | TargetPlanetId=" + (npc.TargetPlanetId ?? string.Empty) +
               " | TargetNpcId=" + (npc.CurrentTargetRuntimeNpcId ?? string.Empty);
    }

    private string BuildNpcRoutePerfComparisonLogFields(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition,
        IReadOnlyList<Vector3> path,
        float pathLength)
    {
        if (npc == null)
        {
            return " | RouteCompareAvailable=False" +
                   " | RouteHash=0" +
                   " | PreviousRouteHash=0" +
                   " | RouteChanged=False" +
                   " | RouteChangeStrength=UNKNOWN" +
                   " | RouteCompareContextChanged=False" +
                   " | RouteTailMatchPercent=0" +
                   " | RouteStartDelta=0" +
                   " | RouteDestinationDelta=0" +
                   " | PreviousPathLength=0" +
                   " | RouteLengthDelta=0" +
                   " | RouteLengthDeltaPercent=0" +
                   " | PreviousPathCount=0" +
                   " | RoutePointCountDelta=0";
        }

        bool hasPath =
            path != null &&
            path.Count > 1;

        if (!hasPath)
        {
            return " | RouteCompareAvailable=False" +
                   " | RouteHash=0" +
                   " | PreviousRouteHash=0" +
                   " | RouteChanged=False" +
                   " | RouteChangeStrength=NO_ROUTE" +
                   " | RouteCompareContextChanged=False" +
                   " | RouteTailMatchPercent=0" +
                   " | RouteStartDelta=0" +
                   " | RouteDestinationDelta=0" +
                   " | PreviousPathLength=0" +
                   " | RouteLengthDelta=0" +
                   " | RouteLengthDeltaPercent=0" +
                   " | PreviousPathCount=0" +
                   " | RoutePointCountDelta=0";
        }

        int pathCount = path.Count;
        int routeHash = CalculateRoutePathHash(path);

        Vector3 startPosition = path[0];
        Vector3 finalPathPoint = path[path.Count - 1];

        string contextHash =
            BuildNpcRoutePerfContextHash(
                npc,
                destinationPosition);

        bool hasPrevious =
            _npcRoutePerfSnapshots.TryGetValue(
                npc.RuntimeNpcId,
                out NpcRoutePerfSnapshot previous) &&
            previous != null &&
            previous.Path.Count > 1;

        bool contextChanged =
            hasPrevious &&
            previous.ContextHash != contextHash;

        bool routeChanged =
            !hasPrevious ||
            previous.RouteHash != routeHash ||
            previous.PathCount != pathCount;

        float previousPathLength =
            hasPrevious
                ? previous.PathLength
                : 0f;

        int previousPathCount =
            hasPrevious
                ? previous.PathCount
                : 0;

        float routeLengthDelta =
            hasPrevious
                ? Mathf.Abs(pathLength - previous.PathLength)
                : 0f;

        float routeLengthDeltaPercent =
            hasPrevious && previous.PathLength > 0.001f
                ? routeLengthDelta / previous.PathLength * 100f
                : 0f;

        float routeStartDelta =
            hasPrevious
                ? Vector3.Distance(startPosition, previous.StartPosition)
                : 0f;

        float routeDestinationDelta =
            hasPrevious
                ? Vector3.Distance(finalPathPoint, previous.DestinationPosition)
                : 0f;

        int routePointCountDelta =
            hasPrevious
                ? pathCount - previous.PathCount
                : 0;

        float routeTailMatchPercent =
            hasPrevious
                ? CalculateRouteTailMatchPercent(previous.Path, path, 1f)
                : 0f;

        string routeChangeStrength =
            GetRouteChangeStrength(
                hasPrevious,
                routeChanged,
                contextChanged,
                routeDestinationDelta,
                routeLengthDeltaPercent,
                routePointCountDelta,
                routeTailMatchPercent);

        SaveNpcRoutePerfSnapshot(
            npc.RuntimeNpcId,
            contextHash,
            routeHash,
            pathLength,
            path);

        return " | RouteCompareAvailable=True" +
               " | RouteHash=" + routeHash +
               " | PreviousRouteHash=" + (hasPrevious ? previous.RouteHash : 0) +
               " | RouteChanged=" + routeChanged +
               " | RouteChangeStrength=" + routeChangeStrength +
               " | RouteCompareContextChanged=" + contextChanged +
               " | RouteTailMatchPercent=" + routeTailMatchPercent.ToString("0.##") +
               " | RouteStartDelta=" + routeStartDelta.ToString("0.###") +
               " | RouteDestinationDelta=" + routeDestinationDelta.ToString("0.###") +
               " | PreviousPathLength=" + previousPathLength.ToString("0.###") +
               " | RouteLengthDelta=" + routeLengthDelta.ToString("0.###") +
               " | RouteLengthDeltaPercent=" + routeLengthDeltaPercent.ToString("0.##") +
               " | PreviousPathCount=" + previousPathCount +
               " | RoutePointCountDelta=" + routePointCountDelta;
    }

    private void SaveNpcRoutePerfSnapshot(
        string runtimeNpcId,
        string contextHash,
        int routeHash,
        float pathLength,
        IReadOnlyList<Vector3> path)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId) ||
            path == null ||
            path.Count <= 1)
        {
            return;
        }

        if (!_npcRoutePerfSnapshots.TryGetValue(
                runtimeNpcId,
                out NpcRoutePerfSnapshot snapshot) ||
            snapshot == null)
        {
            snapshot = new NpcRoutePerfSnapshot();
            _npcRoutePerfSnapshots[runtimeNpcId] = snapshot;
        }

        snapshot.ContextHash = contextHash;
        snapshot.RouteHash = routeHash;
        snapshot.PathLength = pathLength;
        snapshot.PathCount = path.Count;
        snapshot.StartPosition = path[0];
        snapshot.DestinationPosition = path[path.Count - 1];

        snapshot.Path.Clear();

        for (int i = 0; i < path.Count; i++)
        {
            snapshot.Path.Add(path[i]);
        }
    }

    private string GetRouteChangeStrength(
        bool hasPrevious,
        bool routeChanged,
        bool contextChanged,
        float routeDestinationDelta,
        float routeLengthDeltaPercent,
        int routePointCountDelta,
        float routeTailMatchPercent)
    {
        if (!hasPrevious)
            return "FIRST_BUILD";

        if (!routeChanged && !contextChanged)
            return "UNCHANGED";

        if (contextChanged)
            return "CONTEXT_CHANGED";

        if (routeDestinationDelta <= 1f &&
            routeTailMatchPercent >= 95f &&
            routeLengthDeltaPercent <= 5f &&
            routePointCountDelta == 0)
        {
            return "ALMOST_SAME";
        }

        if (routeDestinationDelta <= 1f &&
            routeTailMatchPercent >= 80f)
        {
            return "SAME_DEST_SIMILAR_TAIL";
        }

        if (routeDestinationDelta <= 1f &&
            routeLengthDeltaPercent <= 10f)
        {
            return "SAME_DEST_LENGTH_CLOSE";
        }

        if (routeDestinationDelta <= 1f)
            return "SAME_DEST_CHANGED_ROUTE";

        return "DESTINATION_CHANGED";
    }

    private float CalculateRouteTailMatchPercent(
        IReadOnlyList<Vector3> previousPath,
        IReadOnlyList<Vector3> currentPath,
        float tolerance)
    {
        if (previousPath == null ||
            currentPath == null ||
            previousPath.Count == 0 ||
            currentPath.Count == 0)
        {
            return 0f;
        }

        int comparableCount =
            Mathf.Min(
                previousPath.Count,
                currentPath.Count);

        if (comparableCount <= 0)
            return 0f;

        int matchedCount = 0;
        float toleranceSqr = tolerance * tolerance;

        for (int i = 0; i < comparableCount; i++)
        {
            Vector3 previousPoint =
                previousPath[previousPath.Count - 1 - i];

            Vector3 currentPoint =
                currentPath[currentPath.Count - 1 - i];

            if ((previousPoint - currentPoint).sqrMagnitude <= toleranceSqr)
                matchedCount++;
            else
                break;
        }

        return matchedCount * 100f / comparableCount;
    }

    private string BuildNpcRoutePerfContextHash(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition)
    {
        if (npc == null)
            return string.Empty;

        return npc.CurrentSystemId + "|" +
               npc.CurrentBehavior + "|" +
               npc.TravelState + "|" +
               (npc.TargetSystemId ?? string.Empty) + "|" +
               (npc.TargetPlanetId ?? string.Empty) + "|" +
               (npc.CurrentTargetRuntimeNpcId ?? string.Empty) + "|" +
               GetNpcRouteTargetKind(npc) + "|" +
               FormatVector3ForHash(destinationPosition);
    }

    private int CalculateRoutePathHash(IReadOnlyList<Vector3> path)
    {
        if (path == null || path.Count == 0)
            return 0;

        unchecked
        {
            int hash = 17;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 point = path[i];

                hash = hash * 31 + Mathf.RoundToInt(point.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(point.y * 100f);
                hash = hash * 31 + Mathf.RoundToInt(point.z * 100f);
            }

            return hash;
        }
    }

    private string FormatVector3ForHash(Vector3 value)
    {
        return Mathf.RoundToInt(value.x * 100f) + "," +
               Mathf.RoundToInt(value.y * 100f) + "," +
               Mathf.RoundToInt(value.z * 100f);
    }

    private bool ShouldNeverRefreshNpcFixedRouteUntilArrival(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        string routeTargetKind =
            GetNpcRouteTargetKind(npc);

        return routeTargetKind == "FIXED_SYSTEM_EXIT_POINT" ||
               routeTargetKind == "FIXED_PATROL_POINT";
    }

    private bool IsNpcFixedRouteStillReusable(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        Vector3 finalTargetPosition)
    {
        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        if (!IsSameNpcMovementRouteContext(routeState, npc))
            return false;

        if (!ShouldNeverRefreshNpcFixedRouteUntilArrival(npc))
            return false;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                finalTargetPosition);

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        if (totalRouteLength <= arrivalThreshold)
            return false;

        if (routeState.DistanceTravelled >= totalRouteLength - arrivalThreshold)
            return false;

        float destinationDelta =
            Vector3.Distance(
                routeState.Destination,
                finalTargetPosition);

        return destinationDelta <= 1f;
    }

    private bool TryApplySharedPlannerToNpcRawFallbackRoute(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        float arrivalThreshold,
        SystemShipRouteResult2A routeResult,
        out string sharedPlannerLogFields)
    {
        sharedPlannerLogFields = string.Empty;

        if (npc == null ||
            routeResult == null ||
            routeResult.Path == null ||
            routeResult.Path.Count <= 1 ||
            !routeResult.UsedRawSafeFallback ||
            _sharedRoutePlanner == null)
        {
            return false;
        }

        if (!ShouldUseSharedPlannerForNpcRawFallback(
                npc,
                startFacingDirection,
                routeResult))
        {
            return false;
        }

        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        SystemSharedShipRoutePlanRequest2A request =
            new SystemSharedShipRoutePlanRequest2A
            {
                SystemId = npc.CurrentSystemId,
                Waypoints = routeResult.Path,
                StartFacingDirection = startFacingDirection,
                Speed = Mathf.Max(0f, npc.Speed),
                TurnRadius = GetNpcRouteTurnRadius(npc),
                ArrivalDistanceThreshold = arrivalThreshold,
                RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
                MaxRoutePlanSteps = NpcRoutePlanMaxSteps,
                StraightExitAngleDegrees = GetNpcRouteStraightExitAngleDegrees(),
                TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
                SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
                MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
                MinTurnRadiusAbsolute = movementConfig != null ? movementConfig.MinRouteTurnRadiusAbsolute : 30f,
                SunAvoidanceSafetyMargin = SunAvoidanceSafetyMargin,
                SunAvoidanceTurnRouteReserveMultiplier = 1.5f,
                DebugLog = ShouldLogShipRouteInternals()
                    ? Bootstrapper.Instance.CreateDebugLogAction(DebugLogChannel.ShipRoute)
                    : null,
                DebugPrefix = "[NpcSharedRoutePlanner] "
            };

        bool built =
            _sharedRoutePlanner.TryBuildTurnRadiusPreviewRoute(
                request,
                _sharedRoutePlanResult);

        sharedPlannerLogFields =
            " | SharedPlannerTried=True" +
            " | SharedPlannerBuilt=" + built +
            " | SharedPlannerSource=" + (_sharedRoutePlanResult.Source ?? string.Empty) +
            " | SharedPlannerRejectReason=" + (_sharedRoutePlanResult.RejectReason ?? string.Empty) +
            " | SharedPlannerAttempts=" + _sharedRoutePlanResult.AttemptCount +
            " | SharedPlannerStartTurnAngle=" + _sharedRoutePlanResult.StartTurnAngleDegrees.ToString("0.###") +
            " | SharedPlannerPathCount=" + _sharedRoutePlanResult.Path.Count +
            " | SharedPlannerPathLength=" + _sharedRoutePlanResult.PathLength.ToString("0.###") +
            " | SharedPlannerMaxAllowedRouteLength=" + _sharedRoutePlanResult.MaxAllowedRouteLength.ToString("0.###") +
            " | SharedPlannerTurnRadius=" + request.TurnRadius.ToString("0.###") +
            " | SharedPlannerEffectiveTurnRadius=" + _sharedRoutePlanResult.EffectiveTurnRadius.ToString("0.###") +
            " | SharedPlannerTurnRadiusFactor=" + _sharedRoutePlanResult.TurnRadiusFactor.ToString("0.###") +
            " | SharedPlannerSpeed=" + request.Speed.ToString("0.###") +
            " | SharedPlannerEffectiveSpeed=" + _sharedRoutePlanResult.EffectiveSpeed.ToString("0.###") +
            " | SharedPlannerSpeedFactor=" + _sharedRoutePlanResult.SpeedFactor.ToString("0.###");

        if (!built ||
            _sharedRoutePlanResult.Path.Count <= 1)
        {
            return false;
        }

        routeResult.Path.Clear();
        routeResult.Path.AddRange(_sharedRoutePlanResult.Path);
        routeResult.PathLength = _sharedRoutePlanResult.PathLength;
        routeResult.TurnRadiusFactor = _sharedRoutePlanResult.TurnRadiusFactor;
        routeResult.SpeedFactor = _sharedRoutePlanResult.SpeedFactor;
        routeResult.EffectiveTurnRadius = _sharedRoutePlanResult.EffectiveTurnRadius;
        routeResult.EffectiveSpeed = _sharedRoutePlanResult.EffectiveSpeed;
        routeResult.UsedRawSafeFallback = false;

        return true;
    }

    private bool ShouldUseSharedPlannerForNpcRawFallback(
        SystemNpcRuntimeState npc,
        Vector2 startFacingDirection,
        SystemShipRouteResult2A routeResult)
    {
        if (npc == null ||
            routeResult == null ||
            routeResult.Path == null ||
            routeResult.Path.Count <= 1)
        {
            return false;
        }

        if (IsNpcCombatOrEnemyRoute(npc))
            return false;

        if (startFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        Vector2 routeStartDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f)
                : GetNpcDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f);

        if (routeStartDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        float startTurnAngle =
            Mathf.Abs(
                Vector2.SignedAngle(
                    startFacingDirection.normalized,
                    routeStartDirection.normalized));

        if (startTurnAngle <= GetNpcRawFallbackMaxStartTurnAngleDegrees(npc))
            return false;

        string routeTargetKind = GetNpcRouteTargetKind(npc);

        return routeTargetKind == "FIXED_SYSTEM_EXIT_POINT" ||
               routeTargetKind == "FIXED_PATROL_POINT";
    }

    private float GetNpcRouteTurnRadius(SystemNpcRuntimeState npc)
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        float minTurnRadiusAbsolute =
            movementConfig != null
                ? movementConfig.MinRouteTurnRadiusAbsolute
                : 30f;

        float turnRadius =
            Mathf.Max(
                0f,
                npc != null ? npc.TurnRadius : 0f);

        if (minTurnRadiusAbsolute > 0f)
        {
            turnRadius =
                Mathf.Max(
                    turnRadius,
                    minTurnRadiusAbsolute);
        }

        return turnRadius;
    }

    private string GetNpcRoutePipelineLogFields(SystemNpcRuntimeState npc)
    {
        string routeTargetKind =
            GetNpcRouteTargetKind(npc);

        return
            " | RoutePipeline=SHARED_SHIP_ROUTE_PIPELINE" +
            " | RouteTopLayer=SystemNpcMovementService" +
            " | RouteBuildEntry=TryBuildNpcMovementRoutePath" +
            " | RouteBuildFrom=TryBuildNpcRoutePathFrom" +
            " | SharedBuilder=SystemShipRouteService2A.TryBuildRoute" +
            " | SharedTargetKind=" + GetNpcShipRouteTargetKind2A(npc) +
            " | RoutePostProcessor=AcceptSharedRouteResult" +
            " | LegacyNpcRawFallbackRejectors=False" +
            " | LegacyNpcTurnSpikeRejectors=False" +
            " | RouteTargetKind=" + routeTargetKind;
    }

    private void LogNpcRouteDecisionPerf(
        string decision,
        SystemNpcRuntimeState npc,
        Vector3 rawFinalTargetPosition,
        Vector3 finalTargetPosition,
        bool hasActiveRoute,
        bool hasReusableRoute,
        bool isBoundaryAdjusted,
        float distanceToFinalTarget,
        float arrivalThreshold,
        int currentTick,
        NpcMovementRouteState routeState)
    {
        if (npc == null ||
            Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.NpcMovementRouteDecision))
        {
            return;
        }

        float routeLength =
            routeState != null && routeState.Path != null
                ? GetNpcPathLength(routeState.Path)
                : 0f;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovementRouteDecision,
            "[SystemNpcMovementService] SystemNpcMovementService.EnsureTickMovementDirection " + decision +
            " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
            " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
            " | NpcSystemId=" + npc.CurrentSystemId +
            " | Npc=" + npc.RuntimeNpcId +
            " | Behavior=" + npc.CurrentBehavior +
            " | TravelState=" + npc.TravelState +
            GetNpcRouteTargetLogFields(npc) +
            GetNpcRoutePipelineLogFields(npc) +
            " | Tick=" + currentTick +
            " | HasActiveRoute=" + hasActiveRoute +
            " | HasReusableRoute=" + hasReusableRoute +
            " | BoundaryAdjusted=" + isBoundaryAdjusted +
            " | RawDestination=" + FormatVector3(rawFinalTargetPosition) +
            " | Destination=" + FormatVector3(finalTargetPosition) +
            " | DistanceToDestination=" + distanceToFinalTarget.ToString("0.###") +
            " | ArrivalThreshold=" + arrivalThreshold.ToString("0.###") +
            " | ActiveRoutePathCount=" + (routeState != null && routeState.Path != null ? routeState.Path.Count : 0) +
            " | ActiveRouteLength=" + routeLength.ToString("0.###") +
            " | ActiveRouteDistanceTravelled=" + (routeState != null ? routeState.DistanceTravelled.ToString("0.###") : "0") +
            " | ActiveRouteDestination=" + (routeState != null ? FormatVector3(routeState.Destination) : "None") +
            " | Ms=0.00");
    }

    private bool IsNpcMovementLoadAnalyticsEnabled()
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsPerformanceLogEnabled(
                   DebugLogPerformanceArea.NpcMovementLoadAnalytics);
    }

    private void LogNpcMovementLoadAnalyticsIfNeeded(
        int currentTick,
        float deltaTime,
        double totalMs,
        double getNpcsMs,
        double loopMs,
        string scope,
        string currentSystemId,
        string tickSystemId,
        int galaxyNpcsTotal,
        int galaxyNpcsAlive,
        int currentSystemNpcsAlive,
        int tickSystemNpcsAlive,
        int movedCount,
        int blockedCount,
        int fixedPatrolPointCount,
        int fixedSystemExitPointCount,
        int movingPlanetCount,
        int movingNpcOrEnemyCount,
        int fixedMapPointCount,
        int unknownRouteTargetCount,
        double fixedPatrolPointMs,
        double fixedSystemExitPointMs,
        double movingPlanetMs,
        double movingNpcOrEnemyMs,
        double fixedMapPointMs,
        double unknownRouteTargetMs,
        string dominantRouteTargetKind,
        double dominantRouteTargetMs,
        double maxNpcMs,
        string maxNpcId,
        string maxNpcRouteTargetKind,
        string maxNpcRouteTargetMoveKind,
        double maxFixedSystemExitPointNpcMs,
        string maxFixedSystemExitPointNpcId,
        string maxFixedSystemExitPointBehavior,
        string maxFixedSystemExitPointTravelState,
        string maxFixedSystemExitPointTargetSystemId,
        string maxFixedSystemExitPointTargetPlanetId,
        string maxFixedSystemExitPointRouteTargetKindAfter,
        string maxFixedSystemExitPointRouteTargetMoveKindAfter,
        NpcDetailedMovementPhaseStats phaseStats,
        NpcDetailedMovementPhaseStats maxFixedSystemExitPointPhaseStats)
    {
        if (deltaTime <= 0f)
            return;

        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        if (debugLogConfig == null)
            return;

        float frameMs = deltaTime * 1000f;
        float approxFps = 1f / deltaTime;

        float fpsWarningThreshold =
            debugLogConfig.NpcMovementLoadAnalyticsFpsWarningThreshold;

        float fpsCriticalThreshold =
            debugLogConfig.NpcMovementLoadAnalyticsFpsCriticalThreshold;

        float tickWarningMs =
            debugLogConfig.NpcMovementLoadAnalyticsTickWarningMs;

        int regularLogIntervalTicks =
            debugLogConfig.NpcMovementLoadAnalyticsRegularLogIntervalTicks;

        bool fpsNear30 = approxFps <= fpsWarningThreshold;
        bool fpsBelow30 = approxFps <= fpsCriticalThreshold;
        bool npcTickExpensive = totalMs >= tickWarningMs;
        bool regularSample =
            regularLogIntervalTicks > 0 &&
            currentTick % regularLogIntervalTicks == 0;

        if (!fpsNear30 && !npcTickExpensive && !regularSample)
            return;

        string reason =
            fpsBelow30
                ? "FPS_BELOW_30"
                : fpsNear30
                    ? "FPS_NEAR_30"
                    : npcTickExpensive
                        ? "NPC_TICK_EXPENSIVE"
                        : "REGULAR_SAMPLE";

        string phaseFields = string.Empty;

        if (phaseStats != null)
        {
            phaseFields =
                " | DominantPhase=" + phaseStats.GetDominantPhaseName() +
                " | DominantPhaseMs=" + phaseStats.GetDominantPhaseMs().ToString("F2") +
                " | EnsureDirectionMs=" + phaseStats.EnsureDirectionMs.ToString("F2") +
                " | RouteLookupMs=" + phaseStats.RouteLookupMs.ToString("F2") +
                " | StartTurnMs=" + phaseStats.StartTurnMs.ToString("F2") +
                " | ArrivalThresholdMs=" + phaseStats.ArrivalThresholdMs.ToString("F2") +
                " | PathLengthMs=" + phaseStats.PathLengthMs.ToString("F2") +
                " | DistanceMathMs=" + phaseStats.DistanceMathMs.ToString("F2") +
                " | PointOnPathMs=" + phaseStats.PointOnPathMs.ToString("F2") +
                " | DirectionOnPathMs=" + phaseStats.DirectionOnPathMs.ToString("F2") +
                " | ApplyStateMs=" + phaseStats.ApplyStateMs.ToString("F2") +
                " | PublishMs=" + phaseStats.PublishMs.ToString("F2") +
                " | ArrivalCheckMs=" + phaseStats.ArrivalCheckMs.ToString("F2") +
                " | CompleteMs=" + phaseStats.CompleteMs.ToString("F2") +
                " | ClearRouteMs=" + phaseStats.ClearRouteMs.ToString("F2") +
                " | RouteMissing=" + phaseStats.RouteMissingCount +
                " | ArrivedAfterEnsureDirection=" + phaseStats.ArrivedAfterEnsureDirectionCount +
                " | StartTurnConsumed=" + phaseStats.StartTurnConsumedCount +
                " | Completed=" + phaseStats.CompletedCount +
                " | ClearedRoute=" + phaseStats.ClearedRouteCount +
                " | PublishedPositionChanged=" + phaseStats.PublishedPositionChangedCount +
                " | PathPointCountTotal=" + phaseStats.PathPointCountTotal +
                " | MaxPathPointCount=" + phaseStats.MaxPathPointCount;
        }

        string fixedExitPhaseFields = string.Empty;

        if (maxFixedSystemExitPointPhaseStats != null &&
            maxFixedSystemExitPointNpcMs > 0d)
        {
            fixedExitPhaseFields =
                " | FixedExitMaxNpcDominantPhase=" + maxFixedSystemExitPointPhaseStats.GetDominantPhaseName() +
                " | FixedExitMaxNpcDominantPhaseMs=" + maxFixedSystemExitPointPhaseStats.GetDominantPhaseMs().ToString("F2") +
                " | FixedExitMaxNpcEnsureDirectionMs=" + maxFixedSystemExitPointPhaseStats.EnsureDirectionMs.ToString("F2") +
                " | FixedExitMaxNpcRouteLookupMs=" + maxFixedSystemExitPointPhaseStats.RouteLookupMs.ToString("F2") +
                " | FixedExitMaxNpcStartTurnMs=" + maxFixedSystemExitPointPhaseStats.StartTurnMs.ToString("F2") +
                " | FixedExitMaxNpcArrivalThresholdMs=" + maxFixedSystemExitPointPhaseStats.ArrivalThresholdMs.ToString("F2") +
                " | FixedExitMaxNpcPathLengthMs=" + maxFixedSystemExitPointPhaseStats.PathLengthMs.ToString("F2") +
                " | FixedExitMaxNpcDistanceMathMs=" + maxFixedSystemExitPointPhaseStats.DistanceMathMs.ToString("F2") +
                " | FixedExitMaxNpcPointOnPathMs=" + maxFixedSystemExitPointPhaseStats.PointOnPathMs.ToString("F2") +
                " | FixedExitMaxNpcDirectionOnPathMs=" + maxFixedSystemExitPointPhaseStats.DirectionOnPathMs.ToString("F2") +
                " | FixedExitMaxNpcApplyStateMs=" + maxFixedSystemExitPointPhaseStats.ApplyStateMs.ToString("F2") +
                " | FixedExitMaxNpcPublishMs=" + maxFixedSystemExitPointPhaseStats.PublishMs.ToString("F2") +
                " | FixedExitMaxNpcArrivalCheckMs=" + maxFixedSystemExitPointPhaseStats.ArrivalCheckMs.ToString("F2") +
                " | FixedExitMaxNpcCompleteMs=" + maxFixedSystemExitPointPhaseStats.CompleteMs.ToString("F2") +
                " | FixedExitMaxNpcClearRouteMs=" + maxFixedSystemExitPointPhaseStats.ClearRouteMs.ToString("F2") +
                " | FixedExitMaxNpcRouteMissing=" + maxFixedSystemExitPointPhaseStats.RouteMissingCount +
                " | FixedExitMaxNpcArrivedAfterEnsureDirection=" + maxFixedSystemExitPointPhaseStats.ArrivedAfterEnsureDirectionCount +
                " | FixedExitMaxNpcStartTurnConsumed=" + maxFixedSystemExitPointPhaseStats.StartTurnConsumedCount +
                " | FixedExitMaxNpcCompleted=" + maxFixedSystemExitPointPhaseStats.CompletedCount +
                " | FixedExitMaxNpcClearedRoute=" + maxFixedSystemExitPointPhaseStats.ClearedRouteCount +
                " | FixedExitMaxNpcPublishedPositionChanged=" + maxFixedSystemExitPointPhaseStats.PublishedPositionChangedCount +
                " | FixedExitMaxNpcPathPointCountTotal=" + maxFixedSystemExitPointPhaseStats.PathPointCountTotal +
                " | FixedExitMaxNpcMaxPathPointCount=" + maxFixedSystemExitPointPhaseStats.MaxPathPointCount +
                " | FixedExitEnsureDecision=" + (maxFixedSystemExitPointPhaseStats.EnsureDecision ?? string.Empty) +
                " | FixedExitEnsureEntryRouteTargetKind=" + (maxFixedSystemExitPointPhaseStats.EnsureEntryRouteTargetKind ?? string.Empty) +
                " | FixedExitEnsureEntryRouteTargetMoveKind=" + (maxFixedSystemExitPointPhaseStats.EnsureEntryRouteTargetMoveKind ?? string.Empty) +
                " | FixedExitEnsureExitRouteTargetKind=" + (maxFixedSystemExitPointPhaseStats.EnsureExitRouteTargetKind ?? string.Empty) +
                " | FixedExitEnsureExitRouteTargetMoveKind=" + (maxFixedSystemExitPointPhaseStats.EnsureExitRouteTargetMoveKind ?? string.Empty) +
                " | FixedExitEnsureEntryIsOnPlanet=" + maxFixedSystemExitPointPhaseStats.EnsureEntryIsOnPlanet +
                " | FixedExitEnsureEntryWaitingForInitialRouteBuild=" + maxFixedSystemExitPointPhaseStats.EnsureEntryWaitingForInitialRouteBuild +
                " | FixedExitEnsureEntryReleaseFromPlanetAfterInitialRouteBuild=" + maxFixedSystemExitPointPhaseStats.EnsureEntryReleaseFromPlanetAfterInitialRouteBuild +
                " | FixedExitEnsureEntryHasActiveRoute=" + maxFixedSystemExitPointPhaseStats.EnsureEntryHasActiveRoute +
                " | FixedExitEnsureEntryHasSameContextRoute=" + maxFixedSystemExitPointPhaseStats.EnsureEntryHasSameContextRoute +
                " | FixedExitEnsureEntryRouteWasReusable=" + maxFixedSystemExitPointPhaseStats.EnsureEntryRouteWasReusable +
                " | FixedExitEnsureEntryRoutePathCount=" + maxFixedSystemExitPointPhaseStats.EnsureEntryRoutePathCount +
                " | FixedExitEnsureBuiltRoute=" + maxFixedSystemExitPointPhaseStats.EnsureBuiltRoute +
                " | FixedExitEnsureBuiltRoutePathCount=" + maxFixedSystemExitPointPhaseStats.EnsureBuiltRoutePathCount +
                " | FixedExitEnsureBoundaryAdjusted=" + maxFixedSystemExitPointPhaseStats.EnsureBoundaryAdjusted +
                " | FixedExitEnsureDistanceToFinalTarget=" + maxFixedSystemExitPointPhaseStats.EnsureDistanceToFinalTarget.ToString("0.###") +
                " | FixedExitEnsureArrivalThreshold=" + maxFixedSystemExitPointPhaseStats.EnsureArrivalThreshold.ToString("0.###") +
                " | FixedExitEnsureRouteReuseDistanceThreshold=" + maxFixedSystemExitPointPhaseStats.EnsureRouteReuseDistanceThreshold.ToString("0.###") +
                " | FixedExitEnsureResolveTargetMs=" + maxFixedSystemExitPointPhaseStats.EnsureResolveTargetMs.ToString("F2") +
                " | FixedExitEnsureBoundaryMs=" + maxFixedSystemExitPointPhaseStats.EnsureBoundaryMs.ToString("F2") +
                " | FixedExitEnsureArrivalBeforeBuildMs=" + maxFixedSystemExitPointPhaseStats.EnsureArrivalBeforeBuildMs.ToString("F2") +
                " | FixedExitEnsureActiveRouteLookupMs=" + maxFixedSystemExitPointPhaseStats.EnsureActiveRouteLookupMs.ToString("F2") +
                " | FixedExitEnsureReuseChecksMs=" + maxFixedSystemExitPointPhaseStats.EnsureReuseChecksMs.ToString("F2") +
                " | FixedExitEnsureInitialLimitMs=" + maxFixedSystemExitPointPhaseStats.EnsureInitialLimitMs.ToString("F2") +
                " | FixedExitEnsureBuildRouteMs=" + maxFixedSystemExitPointPhaseStats.EnsureBuildRouteMs.ToString("F2") +
                " | FixedExitEnsureCreateRouteStateMs=" + maxFixedSystemExitPointPhaseStats.EnsureCreateRouteStateMs.ToString("F2") +
                " | FixedExitEnsureUpdateTargetMs=" + maxFixedSystemExitPointPhaseStats.EnsureUpdateTargetMs.ToString("F2") +
                " | FixedExitEnsureFallbackMs=" + maxFixedSystemExitPointPhaseStats.EnsureFallbackMs.ToString("F2") +
                " | FixedExitEnsureInitialRouteBuildsUsedBefore=" + maxFixedSystemExitPointPhaseStats.EnsureInitialRouteBuildsUsedBefore +
                " | FixedExitEnsureInitialRouteBuildsUsedAfter=" + maxFixedSystemExitPointPhaseStats.EnsureInitialRouteBuildsUsedAfter +
                " | FixedExitEnsureMaxInitialRouteBuildsPerTick=" + maxFixedSystemExitPointPhaseStats.EnsureMaxInitialRouteBuildsPerTick +
                " | FixedExitEnsureCurrentPlanetId=" + (maxFixedSystemExitPointPhaseStats.EnsureCurrentPlanetId ?? string.Empty) +
                " | FixedExitEnsureTargetSystemId=" + (maxFixedSystemExitPointPhaseStats.EnsureTargetSystemId ?? string.Empty) +
                " | FixedExitEnsureTargetPlanetId=" + (maxFixedSystemExitPointPhaseStats.EnsureTargetPlanetId ?? string.Empty) +
                " | FixedExitEnsureTargetNpcId=" + (maxFixedSystemExitPointPhaseStats.EnsureTargetNpcId ?? string.Empty) +
                " | FixedExitEnsureEntryPosition=" + FormatVector3(maxFixedSystemExitPointPhaseStats.EnsureEntryPosition) +
                " | FixedExitEnsureRawTargetPosition=" + FormatVector3(maxFixedSystemExitPointPhaseStats.EnsureRawTargetPosition) +
                " | FixedExitEnsureFinalTargetPosition=" + FormatVector3(maxFixedSystemExitPointPhaseStats.EnsureFinalTargetPosition) +
                            " | FixedExitCompleteTotalMs=" + maxFixedSystemExitPointPhaseStats.CompleteTotalMs.ToString("F2") +
                " | FixedExitCompletePrecheckMs=" + maxFixedSystemExitPointPhaseStats.CompletePrecheckMs.ToString("F2") +
                " | FixedExitCompleteClearRouteMs=" + maxFixedSystemExitPointPhaseStats.CompleteClearRouteMs.ToString("F2") +
                " | FixedExitCompleteAssignPositionMs=" + maxFixedSystemExitPointPhaseStats.CompleteAssignPositionMs.ToString("F2") +
                " | FixedExitCompleteStateSwitchMs=" + maxFixedSystemExitPointPhaseStats.CompleteStateSwitchMs.ToString("F2") +
                " | FixedExitCompleteSystemTravelMs=" + maxFixedSystemExitPointPhaseStats.CompleteSystemTravelMs.ToString("F2") +
                " | FixedExitCompleteLogMs=" + maxFixedSystemExitPointPhaseStats.CompleteLogMs.ToString("F2") +
                " | FixedExitCompletePublishEventMs=" + maxFixedSystemExitPointPhaseStats.CompletePublishEventMs.ToString("F2") +
                " | FixedExitCompleteBehaviorMs=" + maxFixedSystemExitPointPhaseStats.CompleteBehaviorMs.ToString("F2") +
                " | FixedExitCompleteWasBlocked=" + maxFixedSystemExitPointPhaseStats.CompleteWasBlocked +
                " | FixedExitCompleteWasSystemTravel=" + maxFixedSystemExitPointPhaseStats.CompleteWasSystemTravel +
                " | FixedExitCompleteRouteTargetKind=" + (maxFixedSystemExitPointPhaseStats.CompleteRouteTargetKind ?? string.Empty) +
                " | FixedExitCompleteRouteTargetMoveKind=" + (maxFixedSystemExitPointPhaseStats.CompleteRouteTargetMoveKind ?? string.Empty) +
                " | FixedExitCompleteBehaviorBefore=" + (maxFixedSystemExitPointPhaseStats.CompleteBehaviorBefore ?? string.Empty) +
                " | FixedExitCompleteTravelStateBefore=" + (maxFixedSystemExitPointPhaseStats.CompleteTravelStateBefore ?? string.Empty) +
                " | FixedExitCompleteTravelStateAfter=" + (maxFixedSystemExitPointPhaseStats.CompleteTravelStateAfter ?? string.Empty) +
                " | FixedExitCompleteSystemBefore=" + (maxFixedSystemExitPointPhaseStats.CompleteSystemBefore ?? string.Empty) +
                " | FixedExitCompleteSystemAfter=" + (maxFixedSystemExitPointPhaseStats.CompleteSystemAfter ?? string.Empty) +
                " | FixedExitCompleteTargetSystemId=" + (maxFixedSystemExitPointPhaseStats.CompleteTargetSystemId ?? string.Empty) +
                " | FixedExitCompleteTargetPlanetId=" + (maxFixedSystemExitPointPhaseStats.CompleteTargetPlanetId ?? string.Empty) +
                " | FixedExitCompleteCurrentPlanetBefore=" + (maxFixedSystemExitPointPhaseStats.CompleteCurrentPlanetBefore ?? string.Empty) +
                " | FixedExitCompleteCurrentPlanetAfter=" + (maxFixedSystemExitPointPhaseStats.CompleteCurrentPlanetAfter ?? string.Empty);
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovementLoadAnalytics,
            "[NPC_LOAD_ANALYTICS]" +
            " SimulationPhase=" + GetNpcSimulationPhaseForAnalytics() +
            " Reason=" + reason +
            " | Scope=" + scope +
            " | Tick=" + currentTick +
            " | ApproxFps=" + approxFps.ToString("F1") +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | NpcTickMs=" + totalMs.ToString("F2") +
            " | GetNpcsMs=" + getNpcsMs.ToString("F2") +
            " | LoopMs=" + loopMs.ToString("F2") +
            " | CurrentSystemId=" + currentSystemId +
            " | TickSystemId=" + tickSystemId +
            " | GalaxyNpcsTotal=" + galaxyNpcsTotal +
            " | GalaxyNpcsAlive=" + galaxyNpcsAlive +
            " | CurrentSystemNpcsAlive=" + currentSystemNpcsAlive +
            " | TickSystemNpcsAlive=" + tickSystemNpcsAlive +
            " | Moved=" + movedCount +
            " | Blocked=" + blockedCount +
            " | FixedPatrolPoint=" + fixedPatrolPointCount +
            " | FixedSystemExitPoint=" + fixedSystemExitPointCount +
            " | MovingPlanet=" + movingPlanetCount +
            " | MovingNpcOrEnemy=" + movingNpcOrEnemyCount +
            " | FixedMapPoint=" + fixedMapPointCount +
            " | UnknownRouteTarget=" + unknownRouteTargetCount +
            " | FixedPatrolPointMs=" + fixedPatrolPointMs.ToString("F2") +
            " | FixedSystemExitPointMs=" + fixedSystemExitPointMs.ToString("F2") +
            " | MovingPlanetMs=" + movingPlanetMs.ToString("F2") +
            " | MovingNpcOrEnemyMs=" + movingNpcOrEnemyMs.ToString("F2") +
            " | FixedMapPointMs=" + fixedMapPointMs.ToString("F2") +
            " | UnknownRouteTargetMs=" + unknownRouteTargetMs.ToString("F2") +
            " | DominantRouteTargetKind=" + (dominantRouteTargetKind ?? string.Empty) +
            " | DominantRouteTargetMs=" + dominantRouteTargetMs.ToString("F2") +
            " | MaxNpcMs=" + maxNpcMs.ToString("F2") +
            " | MaxNpc=" + (maxNpcId ?? string.Empty) +
            " | MaxNpcRouteTargetKind=" + (maxNpcRouteTargetKind ?? string.Empty) +
            " | MaxNpcRouteTargetMoveKind=" + (maxNpcRouteTargetMoveKind ?? string.Empty) +
            " | FixedExitIsDominant=" + string.Equals(dominantRouteTargetKind, "FIXED_SYSTEM_EXIT_POINT", StringComparison.Ordinal) +
            " | MaxFixedSystemExitPointNpcMs=" + maxFixedSystemExitPointNpcMs.ToString("F2") +
            " | MaxFixedSystemExitPointNpc=" + (maxFixedSystemExitPointNpcId ?? string.Empty) +
            " | MaxFixedSystemExitPointBehavior=" + (maxFixedSystemExitPointBehavior ?? string.Empty) +
            " | MaxFixedSystemExitPointTravelState=" + (maxFixedSystemExitPointTravelState ?? string.Empty) +
            " | MaxFixedSystemExitPointTargetSystemId=" + (maxFixedSystemExitPointTargetSystemId ?? string.Empty) +
            " | MaxFixedSystemExitPointTargetPlanetId=" + (maxFixedSystemExitPointTargetPlanetId ?? string.Empty) +
            " | MaxFixedSystemExitPointRouteTargetKindAfter=" + (maxFixedSystemExitPointRouteTargetKindAfter ?? string.Empty) +
            " | MaxFixedSystemExitPointRouteTargetMoveKindAfter=" + (maxFixedSystemExitPointRouteTargetMoveKindAfter ?? string.Empty) +
            phaseFields +
            fixedExitPhaseFields);
    }

    private int GetGalaxyNpcTotalCountForPerf()
    {
        if (_runtimeService == null || _runtimeService.Npcs == null)
            return 0;

        return _runtimeService.Npcs.Count;
    }

    private int GetGalaxyAliveNpcCountForPerf()
    {
        if (_runtimeService == null || _runtimeService.Npcs == null)
            return 0;

        int count = 0;

        IReadOnlyList<SystemNpcRuntimeState> npcs = _runtimeService.Npcs;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc != null && npc.IsAlive)
                count++;
        }

        return count;
    }

    private int CountAliveNpcsInSystemForPerf(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId) ||
            _runtimeService == null ||
            _runtimeService.Npcs == null)
        {
            return 0;
        }

        int count = 0;

        IReadOnlyList<SystemNpcRuntimeState> npcs = _runtimeService.Npcs;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc != null &&
                npc.IsAlive &&
                string.Equals(
                    npc.CurrentSystemId,
                    systemId,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private bool IsNpcFixedRouteStillReusableUntilArrival(
    SystemNpcRuntimeState npc,
    NpcMovementRouteState routeState)
    {
        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        if (!IsSameNpcMovementRouteContext(routeState, npc))
            return false;

        if (!ShouldNeverRefreshNpcFixedRouteUntilArrival(npc))
            return false;

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                routeState.Destination);

        if (totalRouteLength <= arrivalThreshold)
            return false;

        return routeState.DistanceTravelled < totalRouteLength - arrivalThreshold;
    }

    private bool TryBuildInitialRouteOrWait(
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        if (npc == null)
            return false;

        TrySyncNpcPositionWithInitialRouteBuildPlanet(
            npc,
            currentTick,
            "BeforeInitialRouteBuild");

        long phaseStartedAt = phaseStats != null ? BeginPerfMeasure() : 0;

        Vector2 direction =
            EnsureTickMovementDirection(npc, currentTick, phaseStats);

        if (phaseStats != null)
            phaseStats.EnsureDirectionMs += EndPerfMeasureMs(phaseStartedAt);

        return direction.sqrMagnitude > DirectionThresholdSqrMagnitude;
    }

    private bool TryConsumeInitialRouteBuildSlot(int currentTick)
    {
        if (_isInitialWarmupRouteBuild)
            return true;

        if (_initialRouteBuildTick != currentTick)
            _initialRouteBuildTick = currentTick;

        int maxInitialRouteBuildsPerTick =
            GetMaxInitialRouteBuildsPerTick();

        if (_initialRouteBuildsUsedThisTick >= maxInitialRouteBuildsPerTick)
            return false;

        _initialRouteBuildsUsedThisTick++;
        return true;
    }

    private int GetMaxInitialRouteBuildsPerTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return DefaultMaxInitialRouteBuildsPerTick;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .MaxInitialRouteBuildsPerTick;
    }

    private bool ShouldThrottleInitialRouteBuild(
        SystemNpcRuntimeState npc,
        bool hasActiveRoute)
    {
        if (_isInitialWarmupRouteBuild)
            return false;

        if (npc == null)
            return false;

        if (hasActiveRoute)
            return false;

        if (IsNpcFinalPlanetApproachRoutePriorityCandidate(npc))
            return false;

        if (ShouldBypassInitialRouteBuildLimit(npc))
            return false;

        string routeTargetKind = GetNpcRouteTargetKind(npc);

        if (routeTargetKind == "MOVING_NPC_OR_ENEMY")
            return false;

        bool isLimitedInitialMovementBehavior =
            npc.CurrentBehavior == SystemNpcBehaviorType.PlanetToPlanetTravel ||
            npc.CurrentBehavior == SystemNpcBehaviorType.TravelToAnotherSystem ||
            npc.CurrentBehavior == SystemNpcBehaviorType.PatrolSystem;

        if (!isLimitedInitialMovementBehavior &&
            !npc.IsOnPlanet &&
            !npc.ReleaseFromPlanetAfterInitialRouteBuild)
        {
            return false;
        }

        if (npc.IsWaitingForInitialRouteBuild)
            return true;

        if (npc.IsOnPlanet)
            return true;

        if (npc.TravelProgress01 > 0.001f)
            return false;

        return isLimitedInitialMovementBehavior;
    }

    private bool ShouldBypassInitialRouteBuildLimit(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return true;

        if (npc.IsFighting)
            return true;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return true;

        if (!string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
            return true;

        if (npc.IsHostileToPlayer)
            return true;

        if (npc.IsAlly &&
            (npc.AllyRole == AllyRole2A.Military ||
             npc.AllyRole == AllyRole2A.Ranger))
        {
            return true;
        }

        return false;
    }

    private void MarkNpcWaitingForInitialRouteBuild(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        if (!npc.IsWaitingForInitialRouteBuild)
        {
            npc.IsWaitingForInitialRouteBuild = true;
            npc.ReleaseFromPlanetAfterInitialRouteBuild = npc.IsOnPlanet;
            npc.InitialRouteBuildPlanetId = npc.IsOnPlanet ? npc.CurrentPlanetId : null;
        }

        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;
    }

    private void ReleaseNpcAfterInitialRouteBuildIfNeeded(
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcMovementRouteState routeState)
    {
        if (npc == null)
            return;

        if (!npc.IsWaitingForInitialRouteBuild &&
            !npc.ReleaseFromPlanetAfterInitialRouteBuild)
        {
            return;
        }

        TrySyncNpcPositionWithInitialRouteBuildPlanet(
            npc,
            currentTick,
            "BeforeInitialRouteRelease");

        npc.IsWaitingForInitialRouteBuild = false;

        if (npc.ReleaseFromPlanetAfterInitialRouteBuild)
        {
            npc.IsOnPlanet = false;

            if (string.Equals(
                    npc.CurrentPlanetId,
                    npc.InitialRouteBuildPlanetId,
                    StringComparison.Ordinal))
            {
                npc.CurrentPlanetId = null;
            }
        }

        npc.ReleaseFromPlanetAfterInitialRouteBuild = false;
        npc.InitialRouteBuildPlanetId = null;

        _eventBus.Publish(new SystemNpcTravelStateChangedEvent(
            npc.RuntimeNpcId,
            npc,
            npc.TravelState,
            npc.CurrentSystemId));

        LogNpcRouteDecisionPerf(
            "INITIAL_ROUTE_BUILD_READY",
            npc,
            npc.TargetPosition,
            routeState != null ? routeState.Destination : npc.TargetPosition,
            routeState != null,
            routeState != null,
            routeState != null && routeState.IsBoundaryEscapeRoute,
            routeState != null
                ? Vector3.Distance(npc.CurrentPosition, routeState.Destination)
                : 0f,
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                routeState != null ? routeState.Destination : npc.TargetPosition),
            currentTick,
            routeState);
    }

    private bool TrySyncNpcPositionWithInitialRouteBuildPlanet(
        SystemNpcRuntimeState npc,
        int currentTick,
        string reason)
    {
        if (npc == null)
            return false;

        if (!npc.ReleaseFromPlanetAfterInitialRouteBuild &&
            !npc.IsWaitingForInitialRouteBuild)
        {
            return false;
        }

        string planetId =
            !string.IsNullOrWhiteSpace(npc.InitialRouteBuildPlanetId)
                ? npc.InitialRouteBuildPlanetId
                : npc.CurrentPlanetId;

        if (string.IsNullOrWhiteSpace(planetId))
            return false;

        if (_configService == null ||
            Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null ||
            !Bootstrapper.Instance.ServiceRegistry.TryGet<IOrbitalMotionService>(
                out IOrbitalMotionService orbitalMotionService))
        {
            return false;
        }

        PlanetConfig planet =
            _configService.GetPlanetConfigById(planetId);

        if (planet == null ||
            planet.PlanetOrbit == null)
        {
            return false;
        }

        Vector3 previousPosition =
            npc.CurrentPosition;

        Vector3 planetPosition =
            orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit);

        planetPosition.z = previousPosition.z;

        float distance =
            Vector3.Distance(
                previousPosition,
                planetPosition);

        if (distance <= 0.001f)
            return false;

        npc.CurrentPosition = planetPosition;
        npc.StartPosition = planetPosition;

        if (npc.CurrentMovementTargetPosition == previousPosition)
            npc.CurrentMovementTargetPosition = planetPosition;

        if (npc.TickMovementTargetPosition == previousPosition)
            npc.TickMovementTargetPosition = planetPosition;

        Debug.Log(
            "[SystemNpcMovementService] NPC_INITIAL_ROUTE_PLANET_SYNC" +
            " | Reason=" + reason +
            " | Npc=" + npc.RuntimeNpcId +
            " | Tick=" + currentTick +
            " | PlanetId=" + planetId +
            " | PreviousPosition=" + FormatVector3(previousPosition) +
            " | PlanetPosition=" + FormatVector3(planetPosition) +
            " | Distance=" + distance.ToString("0.###") +
            " | IsOnPlanet=" + npc.IsOnPlanet +
            " | IsWaitingForInitialRouteBuild=" + npc.IsWaitingForInitialRouteBuild +
            " | ReleaseFromPlanetAfterInitialRouteBuild=" + npc.ReleaseFromPlanetAfterInitialRouteBuild +
            " | InitialRouteBuildPlanetId=" + (npc.InitialRouteBuildPlanetId ?? string.Empty) +
            " | CurrentPlanetId=" + (npc.CurrentPlanetId ?? string.Empty));

        return true;
    }

    private bool TryCompleteMovementOrDelaySystemExit(
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long wrapperStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        long phaseStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        bool shouldDelay =
            ShouldDelayCurrentSystemExitCompletion(
                npc,
                currentTick,
                phaseStats);

        if (phaseStats != null)
            phaseStats.CompleteDelayCheckMs += EndPerfMeasureMs(phaseStartedAt);

        if (shouldDelay)
        {
            if (phaseStats != null)
            {
                phaseStats.CompleteDelayWasApplied = true;
                phaseStats.CompleteWrapperTotalMs += EndPerfMeasureMs(wrapperStartedAt);
            }

            return false;
        }

        phaseStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        CompleteMovement(npc, currentTick, phaseStats);

        if (phaseStats != null)
        {
            phaseStats.CompleteMovementBodyMs += EndPerfMeasureMs(phaseStartedAt);
            phaseStats.CompleteWrapperTotalMs += EndPerfMeasureMs(wrapperStartedAt);
        }

        return true;
    }

    private bool ShouldDelayCurrentSystemExitCompletion(
        SystemNpcRuntimeState npc,
        int currentTick)
    {
        return ShouldDelayCurrentSystemExitCompletion(
            npc,
            currentTick,
            null);
    }

    private bool ShouldDelayCurrentSystemExitCompletion(
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long phaseStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc == null)
        {
            if (phaseStats != null)
            {
                phaseStats.CompleteDelayDecision = "NPC_NULL";
                phaseStats.CompleteDelayCurrentSystemCheckMs += EndPerfMeasureMs(phaseStartedAt);
            }

            return false;
        }

        bool isCurrentSystem =
            IsCurrentSystemForPerf(npc.CurrentSystemId);

        if (phaseStats != null)
            phaseStats.CompleteDelayCurrentSystemCheckMs += EndPerfMeasureMs(phaseStartedAt);

        if (!isCurrentSystem)
        {
            if (phaseStats != null)
                phaseStats.CompleteDelayDecision = "NOT_CURRENT_SYSTEM";

            return false;
        }

        phaseStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        bool isSystemTravel =
            npc.TravelState == SystemNpcTravelState.TravelingToAnotherSystem;

        if (phaseStats != null)
            phaseStats.CompleteDelayTravelStateCheckMs += EndPerfMeasureMs(phaseStartedAt);

        if (!isSystemTravel)
        {
            if (phaseStats != null)
                phaseStats.CompleteDelayDecision = "NOT_SYSTEM_TRAVEL";

            return false;
        }

        phaseStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        string routeTargetKind =
            GetNpcRouteTargetKind(npc);

        if (phaseStats != null)
            phaseStats.CompleteDelayRouteTargetKindMs += EndPerfMeasureMs(phaseStartedAt);

        if (routeTargetKind != "FIXED_SYSTEM_EXIT_POINT")
        {
            if (phaseStats != null)
                phaseStats.CompleteDelayDecision = "NOT_FIXED_SYSTEM_EXIT_POINT";

            return false;
        }

        phaseStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (_systemExitCompletionTick != currentTick)
        {
            _systemExitCompletionTick = currentTick;
            _systemExitCompletionsUsedThisTick = 0;
        }

        int usedBefore =
            _systemExitCompletionsUsedThisTick;

        int maxCompletionsPerTick =
            GetMaxSystemExitCompletionsPerTick();

        if (phaseStats != null)
        {
            phaseStats.CompleteDelayCompletionsUsedBefore = usedBefore;
            phaseStats.CompleteDelayMaxCompletionsPerTick = maxCompletionsPerTick;
        }

        if (_systemExitCompletionsUsedThisTick >= maxCompletionsPerTick)
        {
            if (phaseStats != null)
            {
                phaseStats.CompleteDelayDecision = "DELAY_SYSTEM_EXIT_COMPLETION_LIMIT";
                phaseStats.CompleteDelayCompletionsUsedAfter = _systemExitCompletionsUsedThisTick;
                phaseStats.CompleteDelayCounterMs += EndPerfMeasureMs(phaseStartedAt);
            }

            return true;
        }

        _systemExitCompletionsUsedThisTick++;

        if (phaseStats != null)
        {
            phaseStats.CompleteDelayDecision = "ALLOW_SYSTEM_EXIT_COMPLETION";
            phaseStats.CompleteDelayCompletionsUsedAfter = _systemExitCompletionsUsedThisTick;
            phaseStats.CompleteDelayCounterMs += EndPerfMeasureMs(phaseStartedAt);
        }

        return false;
    }

    private int GetMaxSystemExitCompletionsPerTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return DefaultMaxSystemExitCompletionsPerTick;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .MaxSystemExitCompletionsPerTick;
    }

    private void ResetRouteBuildBudgetForTick(int currentTick)
    {
        if (_routeBuildBudgetTick == currentTick)
            return;

        _routeBuildBudgetTick = currentTick;
        _routeBuildBudgetUsedMs = 0d;
        _routeRefreshBudgetUsedMs = 0d;
    }

    private NpcRouteBuildBudgetKind GetNpcRouteBuildBudgetKind(
        SystemNpcRuntimeState npc,
        bool hasActiveRoute)
    {
        if (npc == null)
            return NpcRouteBuildBudgetKind.None;

        if (IsNpcFinalPlanetApproachRoutePriorityCandidate(npc))
            return NpcRouteBuildBudgetKind.FinalPlanetApproach;

        if (hasActiveRoute)
            return NpcRouteBuildBudgetKind.RefreshExistingRoute;

        if (npc.IsOnPlanet)
            return NpcRouteBuildBudgetKind.NoRouteOnPlanet;

        return NpcRouteBuildBudgetKind.NoRouteInSpace;
    }

    private bool TryEnterRouteBuildBudget(
        NpcRouteBuildBudgetKind budgetKind,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats,
        out string denyReason)
    {
        denyReason = string.Empty;

        if (phaseStats != null)
        {
            phaseStats.EnsureBudgetKind = budgetKind.ToString();
            phaseStats.EnsureBudgetEnterDecision = "START";
        }

        if (_isInitialWarmupRouteBuild)
        {
            if (phaseStats != null)
                phaseStats.EnsureBudgetEnterDecision = "ALLOW_WARMUP";

            return true;
        }

        if (budgetKind == NpcRouteBuildBudgetKind.FinalPlanetApproach)
        {
            if (phaseStats != null)
                phaseStats.EnsureBudgetEnterDecision = "ALLOW_FINAL_PLANET_APPROACH";

            return true;
        }

        long stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (budgetKind == NpcRouteBuildBudgetKind.NoRouteOnPlanet &&
            IsCurrentSystemPlanetLaunchRouteBuildBlockedBySpacePopulation(
                currentTick,
                phaseStats))
        {
            denyReason = "PLANET_LAUNCH_SPACE_POPULATION_LIMIT_EXCEEDED";

            if (phaseStats != null)
                phaseStats.EnsureBudgetEnterDecision = denyReason;

            return false;
        }

        if (phaseStats != null)
            phaseStats.EnsureBudgetEnterPlanetLaunchPopulationMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        ResetRouteBuildBudgetForTick(currentTick);

        if (phaseStats != null)
            phaseStats.EnsureBudgetEnterResetMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        float totalBudgetMs =
            GetRouteBuildTimeBudgetMsPerTick();

        if (totalBudgetMs > 0f &&
            _routeBuildBudgetUsedMs >= totalBudgetMs)
        {
            denyReason = "ROUTE_BUILD_TOTAL_TIME_BUDGET_EXCEEDED";

            if (phaseStats != null)
            {
                phaseStats.EnsureBudgetEnterTotalBudgetMs += EndPerfMeasureMs(stepStartedAt);
                phaseStats.EnsureBudgetEnterDecision = denyReason;
            }

            return false;
        }

        if (phaseStats != null)
            phaseStats.EnsureBudgetEnterTotalBudgetMs += EndPerfMeasureMs(stepStartedAt);

        if (budgetKind == NpcRouteBuildBudgetKind.RefreshExistingRoute)
        {
            stepStartedAt =
                phaseStats != null
                    ? BeginPerfMeasure()
                    : 0L;

            float refreshBudgetMs =
                GetRouteRefreshTimeBudgetMsPerTick();

            if (refreshBudgetMs > 0f &&
                _routeRefreshBudgetUsedMs >= refreshBudgetMs)
            {
                denyReason = "ROUTE_REFRESH_TIME_BUDGET_EXCEEDED";

                if (phaseStats != null)
                {
                    phaseStats.EnsureBudgetEnterRefreshBudgetMs += EndPerfMeasureMs(stepStartedAt);
                    phaseStats.EnsureBudgetEnterDecision = denyReason;
                }

                return false;
            }

            if (phaseStats != null)
                phaseStats.EnsureBudgetEnterRefreshBudgetMs += EndPerfMeasureMs(stepStartedAt);
        }

        if (phaseStats != null)
            phaseStats.EnsureBudgetEnterDecision = "ALLOW";

        return true;
    }

    private bool IsCurrentSystemPlanetLaunchRouteBuildBlockedBySpacePopulation()
    {
        return IsCurrentSystemPlanetLaunchRouteBuildBlockedBySpacePopulation(
            -1,
            null);
    }

    private bool IsCurrentSystemPlanetLaunchRouteBuildBlockedBySpacePopulation(
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        int maxShipsInSpace =
            GetMaxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild();

        if (maxShipsInSpace <= 0)
            return false;

        string currentSystemId =
            GetCurrentSystemIdForPerf();

        if (string.IsNullOrWhiteSpace(currentSystemId))
            return false;

        int shipsInSpace =
            CountCurrentSystemNpcsInSpace(
                currentSystemId,
                currentTick);

        return shipsInSpace >= maxShipsInSpace;
    }


    private int CountCurrentSystemNpcsInSpace(string systemId)
    {
        return CountCurrentSystemNpcsInSpace(
            systemId,
            -1);
    }

    private int CountCurrentSystemNpcsInSpace(
        string systemId,
        int currentTick)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return 0;

        if (currentTick >= 0 &&
            _currentSystemShipsInSpaceBudgetTick == currentTick &&
            string.Equals(
                _currentSystemShipsInSpaceBudgetSystemId,
                systemId,
                StringComparison.Ordinal))
        {
            return _currentSystemShipsInSpaceBudgetCount;
        }

        if (_runtimeService == null)
            return 0;

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _runtimeService.GetAliveNpcsInSystem(systemId);

        if (npcs == null || npcs.Count == 0)
            return 0;

        int count = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc == null)
                continue;

            if (!npc.IsAlive)
                continue;

            if (npc.LifeState != SystemNpcLifeState.Alive)
                continue;

            if (npc.IsOnPlanet)
                continue;

            count++;
        }

        if (currentTick >= 0)
        {
            _currentSystemShipsInSpaceBudgetTick = currentTick;
            _currentSystemShipsInSpaceBudgetSystemId = systemId;
            _currentSystemShipsInSpaceBudgetCount = count;
        }

        return count;
    }

    private int GetMaxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return 0;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .MaxCurrentSystemShipsInSpaceBeforePlanetLaunchRouteBuild;
    }

    private void ConsumeRouteBuildBudget(
     NpcRouteBuildBudgetKind budgetKind,
     double elapsedMs)
    {
        if (elapsedMs <= 0d)
            return;

        if (budgetKind == NpcRouteBuildBudgetKind.FinalPlanetApproach)
            return;

        _routeBuildBudgetUsedMs += elapsedMs;

        if (budgetKind == NpcRouteBuildBudgetKind.RefreshExistingRoute)
            _routeRefreshBudgetUsedMs += elapsedMs;
    }

    private bool TryBuildNpcMovementRoutePathWithBudget(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition,
        List<Vector3> routePath,
        int currentTick,
        NpcRouteBuildBudgetKind routeBuildBudgetKind,
        out float routeDistanceTravelled,
        out double routeBuildElapsedMs)
    {
        long routeBuildBudgetStartedAt =
            BeginPerfMeasure();

        bool builtRoute =
            TryBuildNpcMovementRoutePath(
                npc,
                destinationPosition,
                routePath,
                currentTick,
                out routeDistanceTravelled);

        routeBuildElapsedMs =
            EndPerfMeasureMs(routeBuildBudgetStartedAt);

        ConsumeRouteBuildBudget(
            routeBuildBudgetKind,
            routeBuildElapsedMs);

        return builtRoute;
    }

    private float GetRouteBuildTimeBudgetMsPerTick()
    {
        if (_isInitialWarmupRouteBuild)
            return 0f;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return 0f;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .RouteBuildTimeBudgetMsPerTick;
    }

    private float GetRouteRefreshTimeBudgetMsPerTick()
    {
        if (_isInitialWarmupRouteBuild)
            return 0f;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return 0f;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .RouteRefreshTimeBudgetMsPerTick;
    }

    private bool IsEnsureDirectionRouteBuildBudgetExceeded(
        NpcRouteBuildBudgetKind budgetKind,
        int currentTick,
        out string denyReason)
    {
        denyReason = string.Empty;

        if (_isInitialWarmupRouteBuild)
            return false;

        if (budgetKind == NpcRouteBuildBudgetKind.FinalPlanetApproach)
            return false;

        ResetRouteBuildBudgetForTick(currentTick);

        float totalBudgetMs =
            GetRouteBuildTimeBudgetMsPerTick();

        float safetyMs =
            GetEnsureDirectionRouteBuildBudgetSafetyMs();

        if (totalBudgetMs > 0f &&
            _routeBuildBudgetUsedMs + safetyMs >= totalBudgetMs)
        {
            denyReason = "ENSURE_DIRECTION_TOTAL_ROUTE_BUILD_BUDGET_EXCEEDED";
            return true;
        }

        if (budgetKind == NpcRouteBuildBudgetKind.RefreshExistingRoute)
        {
            float refreshBudgetMs =
                GetRouteRefreshTimeBudgetMsPerTick();

            if (refreshBudgetMs > 0f &&
                _routeRefreshBudgetUsedMs + safetyMs >= refreshBudgetMs)
            {
                denyReason = "ENSURE_DIRECTION_REFRESH_ROUTE_BUILD_BUDGET_EXCEEDED";
                return true;
            }
        }

        return false;
    }

    private float GetEnsureDirectionRouteBuildBudgetSafetyMs()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return 0f;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .EnsureDirectionRouteBuildBudgetSafetyMs;
    }

    private int GetRouteTargetRerollAttempts()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return 6;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .RouteTargetRerollAttempts;
    }

    private float GetRouteTargetRerollMinDistance()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return 1f;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .RouteTargetRerollMinDistance;
    }

    private void RerollNpcRouteTargetAfterBuildFailure(
        SystemNpcRuntimeState npc,
        string failedRouteTargetKind,
        Vector3 failedDestination,
        int currentTick)
    {
        RerollNpcRouteTargetAfterBuildFailure(
            npc,
            failedRouteTargetKind,
            failedDestination,
            currentTick,
            null);
    }

    private void RerollNpcRouteTargetAfterBuildFailure(
        SystemNpcRuntimeState npc,
        string failedRouteTargetKind,
        Vector3 failedDestination,
        int currentTick,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long totalStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc == null)
            return;

        if (phaseStats != null)
            phaseStats.EnsureFallbackRerollKind = failedRouteTargetKind ?? string.Empty;

        bool rerolled = false;

        long stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        switch (failedRouteTargetKind)
        {
            case "FIXED_SYSTEM_EXIT_POINT":
                rerolled =
                    TryRerollNpcSystemExitTarget(
                        npc,
                        failedDestination,
                        phaseStats);
                break;

            case "FIXED_PATROL_POINT":
            case "FIXED_MAP_POINT":
                rerolled =
                    TryRerollNpcMapTarget(
                        npc,
                        failedDestination,
                        phaseStats);
                break;

            case "MOVING_PLANET":
                rerolled =
                    TryRerollNpcPlanetTarget(
                        npc,
                        failedDestination);
                break;
        }

        if (phaseStats != null)
        {
            phaseStats.EnsureFallbackRerollDispatchMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureFallbackRerolled = rerolled;
        }

        if (!rerolled)
        {
            if (phaseStats != null)
                phaseStats.EnsureFallbackRerollTotalMs += EndPerfMeasureMs(totalStartedAt);

            return;
        }

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        ClearNpcMovementRoute(npc.RuntimeNpcId);

        if (phaseStats != null)
            phaseStats.EnsureFallbackRerollClearRouteMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;

        if (phaseStats != null)
            phaseStats.EnsureFallbackRerollResetStateMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        LogNpcRouteDecisionPerf(
            "ROUTE_BUILD_FAILED_TARGET_REROLLED_" + failedRouteTargetKind,
            npc,
            failedDestination,
            npc.TargetPosition,
            false,
            false,
            false,
            Vector3.Distance(npc.CurrentPosition, npc.TargetPosition),
            GetNpcRouteArrivalDistanceThreshold(npc, npc.TargetPosition),
            currentTick,
            null);

        if (phaseStats != null)
        {
            phaseStats.EnsureFallbackRerollLogMs += EndPerfMeasureMs(stepStartedAt);
            phaseStats.EnsureFallbackRerollTotalMs += EndPerfMeasureMs(totalStartedAt);
        }
    }

    private bool TryRerollNpcSystemExitTarget(
        SystemNpcRuntimeState npc,
        Vector3 failedDestination)
    {
        return TryRerollNpcSystemExitTarget(
            npc,
            failedDestination,
            null);
    }

    private bool TryRerollNpcSystemExitTarget(
        SystemNpcRuntimeState npc,
        Vector3 failedDestination,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            if (phaseStats != null)
                phaseStats.EnsureFallbackSystemExitConfigMs += EndPerfMeasureMs(stepStartedAt);

            return false;
        }

        StarSystemConfig currentSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (phaseStats != null)
            phaseStats.EnsureFallbackSystemExitConfigMs += EndPerfMeasureMs(stepStartedAt);

        if (currentSystem == null ||
            currentSystem.Routes == null ||
            currentSystem.Routes.Count == 0)
        {
            return false;
        }

        if (phaseStats != null)
            phaseStats.EnsureFallbackSystemExitRouteCount = currentSystem.Routes.Count;

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        IRouteService routeService =
            Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null
                ? Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>()
                : null;

        if (phaseStats != null)
            phaseStats.EnsureFallbackSystemExitRouteServiceMs += EndPerfMeasureMs(stepStartedAt);

        if (routeService == null)
            return false;

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        int attempts =
            Mathf.Max(
                1,
                GetRouteTargetRerollAttempts());

        int startIndex =
            UnityEngine.Random.Range(
                0,
                currentSystem.Routes.Count);

        float minDistance =
            GetRouteTargetRerollMinDistance();

        RouteConfig fallbackRoute = null;
        StarSystemConfig fallbackTargetSystem = null;
        Vector3 fallbackExitPoint = Vector3.zero;
        Vector3 fallbackEntryPoint = Vector3.zero;

        if (phaseStats != null)
            phaseStats.EnsureFallbackSystemExitSetupMs += EndPerfMeasureMs(stepStartedAt);

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        for (int attempt = 0; attempt < currentSystem.Routes.Count; attempt++)
        {
            if (phaseStats != null)
                phaseStats.EnsureFallbackSystemExitAttemptsChecked++;

            int index =
                (startIndex + attempt) %
                currentSystem.Routes.Count;

            RouteConfig route =
                currentSystem.Routes[index];

            if (route == null ||
                string.IsNullOrWhiteSpace(route.Id))
            {
                continue;
            }

            if (!routeService.IsRouteUnlocked(route.Id))
                continue;

            StarSystemConfig targetSystem =
                route.GetOtherSystem(currentSystem.Id);

            if (targetSystem == null ||
                string.IsNullOrWhiteSpace(targetSystem.Id))
            {
                continue;
            }

            Vector3 exitPoint =
                route.GetExitPoint(currentSystem.Id);

            Vector3 entryPoint =
                route.GetEntryPoint(targetSystem.Id);

            long invalidCheckStartedAt =
                phaseStats != null
                    ? BeginPerfMeasure()
                    : 0L;

            bool invalidRoutePoint =
                IsInvalidNpcRoutePoint(currentSystem, exitPoint) ||
                IsInvalidNpcRoutePoint(targetSystem, entryPoint);

            if (phaseStats != null)
                phaseStats.EnsureFallbackSystemExitInvalidPointCheckMs += EndPerfMeasureMs(invalidCheckStartedAt);

            if (invalidRoutePoint)
                continue;

            fallbackRoute = route;
            fallbackTargetSystem = targetSystem;
            fallbackExitPoint = exitPoint;
            fallbackEntryPoint = entryPoint;

            bool isDifferentTargetSystem =
                !string.Equals(
                    npc.TargetSystemId,
                    targetSystem.Id,
                    StringComparison.Ordinal);

            bool isDifferentExitPoint =
                Vector3.Distance(
                    exitPoint,
                    failedDestination) > minDistance;

            if (!isDifferentTargetSystem &&
                !isDifferentExitPoint &&
                attempt < attempts)
            {
                continue;
            }

            if (phaseStats != null)
                phaseStats.EnsureFallbackSystemExitLoopMs += EndPerfMeasureMs(stepStartedAt);

            stepStartedAt =
                phaseStats != null
                    ? BeginPerfMeasure()
                    : 0L;

            npc.TargetSystemId = targetSystem.Id;
            npc.TargetSystemExitPoint = exitPoint;
            npc.TargetSystemEntryPoint = entryPoint;
            npc.TargetPlanetId = null;
            npc.CurrentTargetRuntimeNpcId = null;
            npc.TargetPosition = exitPoint;
            npc.CurrentMovementTargetPosition = Vector3.zero;
            npc.TickMovementTargetPosition = Vector3.zero;
            npc.TravelProgress01 = 0f;

            if (phaseStats != null)
                phaseStats.EnsureFallbackSystemExitAssignMs += EndPerfMeasureMs(stepStartedAt);

            return true;
        }

        if (phaseStats != null)
            phaseStats.EnsureFallbackSystemExitLoopMs += EndPerfMeasureMs(stepStartedAt);

        if (fallbackRoute == null ||
            fallbackTargetSystem == null)
        {
            return false;
        }

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        npc.TargetSystemId = fallbackTargetSystem.Id;
        npc.TargetSystemExitPoint = fallbackExitPoint;
        npc.TargetSystemEntryPoint = fallbackEntryPoint;
        npc.TargetPlanetId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.TargetPosition = fallbackExitPoint;
        npc.CurrentMovementTargetPosition = Vector3.zero;
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.TravelProgress01 = 0f;

        if (phaseStats != null)
        {
            phaseStats.EnsureFallbackSystemExitUsedFallback = true;
            phaseStats.EnsureFallbackSystemExitAssignMs += EndPerfMeasureMs(stepStartedAt);
        }

        return true;
    }

    private bool TryRerollNpcMapTarget(
        SystemNpcRuntimeState npc,
        Vector3 failedDestination)
    {
        return TryRerollNpcMapTarget(
            npc,
            failedDestination,
            null);
    }

    private bool TryRerollNpcMapTarget(
        SystemNpcRuntimeState npc,
        Vector3 failedDestination,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        long stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        if (npc == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            if (phaseStats != null)
                phaseStats.EnsureFallbackMapConfigMs += EndPerfMeasureMs(stepStartedAt);

            return false;
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (phaseStats != null)
            phaseStats.EnsureFallbackMapConfigMs += EndPerfMeasureMs(stepStartedAt);

        if (starSystem == null)
            return false;

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        bool hasPatrolBounds =
            TryGetNpcPatrolBounds(
                starSystem,
                out Vector3 patrolCenter,
                out float patrolRadius);

        if (phaseStats != null)
            phaseStats.EnsureFallbackMapPatrolBoundsMs += EndPerfMeasureMs(stepStartedAt);

        if (!hasPatrolBounds)
            return false;

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        int attempts =
            Mathf.Max(
                1,
                GetRouteTargetRerollAttempts());

        float minDistance =
            GetRouteTargetRerollMinDistance();

        Vector3 fallbackPosition = Vector3.zero;
        bool hasFallback = false;

        if (phaseStats != null)
        {
            phaseStats.EnsureFallbackMapAttemptsConfigured = attempts;
            phaseStats.EnsureFallbackMapSetupMs += EndPerfMeasureMs(stepStartedAt);
        }

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        for (int i = 0; i < attempts; i++)
        {
            if (phaseStats != null)
                phaseStats.EnsureFallbackMapAttemptsChecked++;

            long randomStartedAt =
                phaseStats != null
                    ? BeginPerfMeasure()
                    : 0L;

            Vector3 candidate =
                GetRandomNpcPatrolPosition(
                    starSystem,
                    patrolCenter,
                    patrolRadius);

            candidate.z = -2f;

            if (phaseStats != null)
                phaseStats.EnsureFallbackMapRandomPositionMs += EndPerfMeasureMs(randomStartedAt);

            if (!IsFinite(candidate))
                continue;

            long invalidCheckStartedAt =
                phaseStats != null
                    ? BeginPerfMeasure()
                    : 0L;

            bool invalidRoutePoint =
                IsInvalidNpcRoutePoint(starSystem, candidate);

            if (phaseStats != null)
                phaseStats.EnsureFallbackMapInvalidPointCheckMs += EndPerfMeasureMs(invalidCheckStartedAt);

            if (invalidRoutePoint)
                continue;

            fallbackPosition = candidate;
            hasFallback = true;

            if (Vector3.Distance(candidate, failedDestination) <= minDistance)
                continue;

            if (phaseStats != null)
                phaseStats.EnsureFallbackMapLoopMs += EndPerfMeasureMs(stepStartedAt);

            stepStartedAt =
                phaseStats != null
                    ? BeginPerfMeasure()
                    : 0L;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;
            npc.TargetPlanetId = null;
            npc.CurrentTargetRuntimeNpcId = null;
            npc.TargetPosition = candidate;
            npc.CurrentMovementTargetPosition = Vector3.zero;
            npc.TickMovementTargetPosition = Vector3.zero;
            npc.TravelProgress01 = 0f;

            if (phaseStats != null)
                phaseStats.EnsureFallbackMapAssignMs += EndPerfMeasureMs(stepStartedAt);

            return true;
        }

        if (phaseStats != null)
            phaseStats.EnsureFallbackMapLoopMs += EndPerfMeasureMs(stepStartedAt);

        if (!hasFallback)
            return false;

        stepStartedAt =
            phaseStats != null
                ? BeginPerfMeasure()
                : 0L;

        npc.TargetSystemId = null;
        npc.TargetSystemExitPoint = Vector3.zero;
        npc.TargetSystemEntryPoint = Vector3.zero;
        npc.TargetPlanetId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.TargetPosition = fallbackPosition;
        npc.CurrentMovementTargetPosition = Vector3.zero;
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.TravelProgress01 = 0f;

        if (phaseStats != null)
        {
            phaseStats.EnsureFallbackMapUsedFallback = true;
            phaseStats.EnsureFallbackMapAssignMs += EndPerfMeasureMs(stepStartedAt);
        }

        return true;
    }

    private bool TryRerollNpcPlanetTarget(
        SystemNpcRuntimeState npc,
        Vector3 failedDestination)
    {
        if (npc == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            return false;
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null ||
            starSystem.PlanetRefs == null ||
            starSystem.PlanetRefs.Length == 0)
        {
            return false;
        }

        int attempts =
            Mathf.Max(
                1,
                GetRouteTargetRerollAttempts());

        int startIndex =
            UnityEngine.Random.Range(
                0,
                starSystem.PlanetRefs.Length);

        float minDistance =
            GetRouteTargetRerollMinDistance();

        PlanetConfig fallbackPlanet = null;
        Vector3 fallbackPosition = Vector3.zero;

        for (int attempt = 0; attempt < starSystem.PlanetRefs.Length; attempt++)
        {
            int index =
                (startIndex + attempt) %
                starSystem.PlanetRefs.Length;

            PlanetConfig planet =
                starSystem.PlanetRefs[index];

            if (planet == null ||
                string.IsNullOrWhiteSpace(planet.Id) ||
                planet.PlanetOrbit == null)
            {
                continue;
            }

            Vector3 planetPosition =
                Bootstrapper.Instance != null &&
                Bootstrapper.Instance.ServiceRegistry != null &&
                Bootstrapper.Instance.ServiceRegistry.TryGet<IOrbitalMotionService>(
                    out IOrbitalMotionService orbitalMotionService)
                    ? orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit)
                    : Vector3.zero;

            planetPosition.z = -2f;

            if (!IsFinite(planetPosition) ||
                IsInvalidNpcRoutePoint(starSystem, planetPosition))
            {
                continue;
            }

            fallbackPlanet = planet;
            fallbackPosition = planetPosition;

            bool isDifferentPlanet =
                !string.Equals(
                    npc.TargetPlanetId,
                    planet.Id,
                    StringComparison.Ordinal);

            bool isDifferentPosition =
                Vector3.Distance(
                    planetPosition,
                    failedDestination) > minDistance;

            if ((!isDifferentPlanet || !isDifferentPosition) &&
                attempt < attempts)
            {
                continue;
            }

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;
            npc.TargetPlanetId = planet.Id;
            npc.CurrentTargetRuntimeNpcId = null;
            npc.TargetPosition = planetPosition;
            npc.CurrentMovementTargetPosition = Vector3.zero;
            npc.TickMovementTargetPosition = Vector3.zero;
            npc.TravelProgress01 = 0f;

            return true;
        }

        if (fallbackPlanet == null)
            return false;

        npc.TargetSystemId = null;
        npc.TargetSystemExitPoint = Vector3.zero;
        npc.TargetSystemEntryPoint = Vector3.zero;
        npc.TargetPlanetId = fallbackPlanet.Id;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.TargetPosition = fallbackPosition;
        npc.CurrentMovementTargetPosition = Vector3.zero;
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.TravelProgress01 = 0f;

        return true;
    }

    private bool TryGetNpcPatrolBounds(
        StarSystemConfig starSystem,
        out Vector3 patrolCenter,
        out float patrolRadius)
    {
        patrolCenter = Vector3.zero;
        patrolRadius = 0f;

        if (starSystem == null ||
            starSystem.PlanetRefs == null ||
            starSystem.PlanetRefs.Length == 0)
        {
            return false;
        }

        PlanetOrbitConfig selectedOrbit = null;

        for (int i = 0; i < starSystem.PlanetRefs.Length; i++)
        {
            PlanetConfig planet =
                starSystem.PlanetRefs[i];

            if (planet == null ||
                planet.PlanetOrbit == null)
            {
                continue;
            }

            if (selectedOrbit == null ||
                planet.PlanetOrbit.OrbitRadius > selectedOrbit.OrbitRadius)
            {
                selectedOrbit = planet.PlanetOrbit;
            }
        }

        if (selectedOrbit == null)
            return false;

        patrolCenter = selectedOrbit.OrbitCenterOffset;
        patrolCenter.z = 0f;
        patrolRadius = Mathf.Max(0f, selectedOrbit.OrbitRadius);

        return patrolRadius > 0f;
    }

    private Vector3 GetRandomNpcPatrolPosition(
        StarSystemConfig starSystem,
        Vector3 patrolCenter,
        float patrolRadius)
    {
        if (starSystem == null ||
            starSystem.Sun == null)
        {
            return patrolCenter + GetRandomNpcPatrolOffset(patrolRadius);
        }

        SunConfig sun =
            starSystem.Sun;

        Vector3 sunCenter =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                0f);

        float sunRadius =
            Mathf.Max(
                0f,
                GetSunWorldSize(sun) * 0.5f);

        float safeRadius =
            sunRadius + SunAvoidanceSafetyMargin;

        int attempts =
            Mathf.Max(
                1,
                GetRouteTargetRerollAttempts());

        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate =
                patrolCenter + GetRandomNpcPatrolOffset(patrolRadius);

            if (Vector3.Distance(candidate, sunCenter) > safeRadius)
                return candidate;
        }

        Vector2 direction =
            UnityEngine.Random.insideUnitCircle.normalized;

        if (direction.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            direction = Vector2.right;

        Vector3 fallback =
            sunCenter +
            new Vector3(
                direction.x,
                direction.y,
                0f) *
            (safeRadius + 1f);

        if (Vector3.Distance(fallback, patrolCenter) > patrolRadius)
        {
            Vector3 fromCenter =
                fallback - patrolCenter;

            if (fromCenter.sqrMagnitude > DirectionThresholdSqrMagnitude)
                fallback = patrolCenter + fromCenter.normalized * patrolRadius;
        }

        return fallback;
    }

    private Vector3 GetRandomNpcPatrolOffset(float radius)
    {
        Vector2 offset =
            UnityEngine.Random.insideUnitCircle *
            Mathf.Max(0f, radius);

        return new Vector3(
            offset.x,
            offset.y,
            0f);
    }

    private bool IsInvalidNpcRoutePoint(
        StarSystemConfig starSystem,
        Vector3 point)
    {
        if (!IsFinite(point))
            return true;

        if (point.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return true;

        if (starSystem == null ||
            starSystem.Sun == null)
        {
            return false;
        }

        SunConfig sun =
            starSystem.Sun;

        Vector3 sunCenter =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                point.z);

        float sunRadius =
            Mathf.Max(
                0f,
                GetSunWorldSize(sun) * 0.5f);

        float safeRadius =
            sunRadius + SunAvoidanceSafetyMargin;

        return Vector3.Distance(point, sunCenter) <= safeRadius;
    }

    public void RunInitialWarmupRoutes(
        StarSystemConfig starSystem,
        int currentTick)
    {
        if (starSystem == null ||
            string.IsNullOrWhiteSpace(starSystem.Id))
        {
            return;
        }

        bool previousInitialWarmupRouteBuild =
            _isInitialWarmupRouteBuild;

        _isInitialWarmupRouteBuild = true;

        try
        {
            _initialRouteBuildTick = currentTick;
            _initialRouteBuildsUsedThisTick = 0;
            ResetRouteBuildBudgetForTick(currentTick);

            Tick(
                starSystem,
                0.0001f,
                currentTick);
        }
        finally
        {
            _isInitialWarmupRouteBuild =
                previousInitialWarmupRouteBuild;

            _initialRouteBuildTick = currentTick;
            _initialRouteBuildsUsedThisTick = 0;
            ResetRouteBuildBudgetForTick(currentTick);
        }
    }

    private void AddNpcBehaviorActionAnalytics(
        SystemNpcBehaviorType behavior,
        NpcDetailedMovementPhaseStats phaseStats)
    {
        if (phaseStats == null)
            return;

        double resolveMovementTargetMs =
            phaseStats.EnsureResolveTargetMs +
            phaseStats.EnsureBoundaryMs +
            phaseStats.EnsureArrivalBeforeBuildMs +
            phaseStats.EnsureActiveRouteLookupMs;

        _behaviorActionAnalytics.Add(
            behavior,
            NpcBehaviorActionKind.ResolveMovementTarget,
            resolveMovementTargetMs);

        _behaviorActionAnalytics.AddResolveTargetDetails(
            behavior,
            phaseStats.TargetResolveStats,
            phaseStats.EnsureBoundaryMs,
            phaseStats.EnsureArrivalBeforeBuildMs,
            phaseStats.EnsureActiveRouteLookupMs);

        bool hasBuildDecision =
            string.Equals(phaseStats.EnsureDecision, "BUILD_NEW_ROUTE_OK", StringComparison.Ordinal) ||
            string.Equals(phaseStats.EnsureDecision, "BUILD_NEW_ROUTE_START_TURN_IN_PLACE", StringComparison.Ordinal) ||
            string.Equals(phaseStats.EnsureDecision, "BUILD_FAILED_WAIT_AFTER_TARGET_REROLL", StringComparison.Ordinal) ||
            string.Equals(phaseStats.EnsureDecision, "BUILD_FAILED_KEEP_OLD_ROUTE", StringComparison.Ordinal);

        if (hasBuildDecision)
        {
            NpcBehaviorActionKind routeActionKind =
                phaseStats.EnsureEntryHasActiveRoute
                    ? NpcBehaviorActionKind.RefreshRoute
                    : NpcBehaviorActionKind.BuildNewRoute;

            _behaviorActionAnalytics.Add(
                behavior,
                routeActionKind,
                phaseStats.EnsureBuildRouteMs);

            _behaviorActionAnalytics.AddRouteBuildDetails(
                behavior,
                phaseStats,
                routeActionKind);
        }

        if (!string.IsNullOrWhiteSpace(phaseStats.EnsureDecision) &&
            phaseStats.EnsureDecision.IndexOf("REROLL", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            _behaviorActionAnalytics.Add(
                behavior,
                NpcBehaviorActionKind.RerollMovementTarget,
                phaseStats.EnsureFallbackMs);
        }

        if (IsNpcBehaviorActionReuseDecision(phaseStats.EnsureDecision))
        {
            _behaviorActionAnalytics.Add(
                behavior,
                NpcBehaviorActionKind.ReuseOldRoute,
                phaseStats.EnsureReuseChecksMs + phaseStats.EnsureUpdateTargetMs);

            _behaviorActionAnalytics.AddRouteReuseDetails(
                behavior,
                phaseStats);
        }

        double moveAlongRouteMs =
            phaseStats.RouteLookupMs +
            phaseStats.StartTurnMs +
            phaseStats.ArrivalThresholdMs +
            phaseStats.PathLengthMs +
            phaseStats.DistanceMathMs +
            phaseStats.PointOnPathMs +
            phaseStats.DirectionOnPathMs +
            phaseStats.ApplyStateMs +
            phaseStats.PublishMs +
            phaseStats.ArrivalCheckMs +
            phaseStats.ClearRouteMs;

        _behaviorActionAnalytics.Add(
            behavior,
            NpcBehaviorActionKind.MoveAlongRoute,
            moveAlongRouteMs);

        _behaviorActionAnalytics.AddMoveAlongRouteDetails(
            behavior,
            phaseStats);

        _behaviorActionAnalytics.Add(
            behavior,
            NpcBehaviorActionKind.CompleteMovement,
            phaseStats.CompleteMs);

        _behaviorActionAnalytics.AddCompleteMovementDetails(
            behavior,
            phaseStats);
    }

    private bool IsNpcBehaviorActionReuseDecision(string decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
            return false;

        return decision.StartsWith("USE_", StringComparison.Ordinal) ||
               decision.StartsWith("SKIP_ALREADY_HAS_TICK_TARGET", StringComparison.Ordinal) ||
               decision.EndsWith("_KEEP_OLD_ROUTE", StringComparison.Ordinal);
    }

    private void LogNpcBehaviorActionAnalytics(
        int currentTick,
        float deltaTime,
        double totalMs,
        string scope,
        string currentSystemId,
        string tickSystemId,
        int npcCount,
        int movedCount,
        int blockedCount,
        int blockedNullCount,
        int blockedNotAliveCount,
        int blockedIsOnPlanetCount,
        int blockedTravelStateIdleCount,
        int blockedTravelStateOnPlanetCount,
        int blockedUnknownCount,
        int blockedInSpaceCount)
    {
        if (!IsNpcMovementLoadAnalyticsEnabled())
            return;

        if (scope != "CURRENT_SYSTEM")
            return;

        DebugLogConfig debugLogConfig =
            GetDebugLogConfig();

        float frameMs =
            deltaTime * 1000f;

        float approxFps =
            deltaTime > 0f
                ? 1f / deltaTime
                : 0f;

        if (!ShouldLogNpcBehaviorActionAnalytics(
                debugLogConfig,
                currentTick,
                approxFps,
                totalMs,
                out bool includeDetailedActionFields))
        {
            return;
        }

        string detailLevel =
            debugLogConfig != null
                ? debugLogConfig.NpcMovementLoadAnalyticsDetailLevel.ToString()
                : string.Empty;

        string actionFields =
            includeDetailedActionFields
                ? _behaviorActionAnalytics.BuildLogFields()
                : string.Empty;

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.NpcMovementLoadAnalytics,
            "[NPC_BEHAVIOR_ACTION_ANALYTICS]" +
            " | UnityFrame=" + Time.frameCount +
            " | SimulationPhase=" + GetNpcSimulationPhaseForAnalytics() +
            " | DetailLevel=" + detailLevel +
            " | DetailedActionFields=" + includeDetailedActionFields +
            " | Tick=" + currentTick +
            " | Scope=" + scope +
            " | CurrentSystemId=" + currentSystemId +
            " | TickSystemId=" + tickSystemId +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | ApproxFps=" + approxFps.ToString("F1") +
            " | NpcTickMs=" + totalMs.ToString("F2") +
            " | Npcs=" + npcCount +
            " | Moved=" + movedCount +
            " | Blocked=" + blockedCount +
            " | BlockedNull=" + blockedNullCount +
            " | BlockedNotAlive=" + blockedNotAliveCount +
            " | BlockedIsOnPlanet=" + blockedIsOnPlanetCount +
            " | BlockedTravelStateIdle=" + blockedTravelStateIdleCount +
            " | BlockedTravelStateOnPlanet=" + blockedTravelStateOnPlanetCount +
            " | BlockedUnknown=" + blockedUnknownCount +
            " | BlockedInSpace=" + blockedInSpaceCount +
            actionFields);
    }

    private string GetNpcSimulationPhaseForAnalytics()
    {
        return _isInitialWarmupRouteBuild
            ? "InitialWarmup"
            : "Runtime";
    }

    private static string BuildNpcShipRouteResultLogFields(
        SystemShipRouteResult2A result)
    {
        if (result == null)
        {
            return " | ShipRouteResultAvailable=False";
        }

        return " | ShipRouteResultAvailable=True" +
               " | ShipRouteBuilt=" + result.Built +
               " | ShipRouteLastRejectReason=" + result.LastRejectReason +
               " | ShipRouteSelectedSource=" + result.SelectedRouteSource +
               " | ShipRouteDestinationCase=" + result.DestinationCase +
               " | ShipRouteDirectCrossesSun=" + result.DirectRouteCrossesSun +
               " | ShipRouteUsedSunAvoidance=" + result.UsedSunAvoidance +
               " | ShipRouteUsedRawFallback=" + result.UsedRawSafeFallback +
               " | ShipRouteCandidateAttempts=" + result.CandidateAttemptCount +
               " | ShipRouteRawFallbackAttempts=" + result.RawFallbackAttemptCount +
               " | ShipRouteWaypointCount=" + result.LastWaypointCount +
               " | ShipRouteWaypointPathLength=" + result.LastWaypointPathLength.ToString("0.###") +
               " | ShipRoutePlannerMaxSteps=" + result.LastPlannerMaxSteps +
               " | ShipRoutePlannerRouteStepDistance=" + result.LastPlannerRouteStepDistance.ToString("0.###") +
               " | ShipRoutePlannerRouteLength=" + result.LastPlannerRouteLength.ToString("0.###") +
               " | ShipRoutePlannerMaxAllowedRouteLength=" + result.LastPlannerMaxAllowedRouteLength.ToString("0.###") +
               " | ShipRouteBasePathLength=" + result.PathLength.ToString("0.###") +
               " | ShipRouteHasSunObstacle=" + result.HasSunObstacle +
               " | ShipRouteSunRadius=" + result.SunRadius.ToString("0.###") +
               " | ShipRouteSunBlockingRadius=" + result.SunBlockingRadius.ToString("0.###") +
               " | ShipRouteStartPushedOutsideSun=" + result.StartWasPushedOutsideSun +
               " | ShipRouteDestinationPushedOutsideSun=" + result.DestinationWasPushedOutsideSun +
               " | ShipRouteEffectiveSpeed=" + result.EffectiveSpeed.ToString("0.###") +
               " | ShipRouteEffectiveTurnRadius=" + result.EffectiveTurnRadius.ToString("0.###") +
               " | ShipRouteSpeedFactor=" + result.SpeedFactor.ToString("0.###") +
               " | ShipRouteTurnRadiusFactor=" + result.TurnRadiusFactor.ToString("0.###");
    }

    private static string BuildNpcTickMovementPhaseLogFields(
        NpcDetailedMovementPhaseStats phaseStats,
        double npcMs)
    {
        if (phaseStats == null)
        {
            return " | TickPhaseStatsAvailable=False";
        }

        double preRouteOrInitialMs =
            phaseStats.TickPreRouteOrInitialMs > 0d
                ? phaseStats.TickPreRouteOrInitialMs
                : phaseStats.EnsureDirectionMs;

        double ensureUpdateTargetInnerMs =
            phaseStats.EnsureUpdateTargetPrecheckMs +
            phaseStats.EnsureUpdateTargetStartTurnMs +
            phaseStats.EnsureUpdateTargetPathLengthMs +
            phaseStats.EnsureUpdateTargetArrivalThresholdMs +
            phaseStats.EnsureUpdateTargetDistanceMathMs +
            phaseStats.EnsureUpdateTargetPointOnPathMs +
            phaseStats.EnsureUpdateTargetAssignMs;

        double ensureInnerTrackedMs =
            phaseStats.EnsureFastRouteTotalMs +
            phaseStats.EnsureResolveTargetMs +
            phaseStats.EnsureBoundaryMs +
            phaseStats.EnsureArrivalBeforeBuildMs +
            phaseStats.EnsureActiveRouteLookupMs +
            phaseStats.EnsureReuseChecksMs +
            phaseStats.EnsureInitialLimitMs +
            phaseStats.EnsureBuildRouteMs +
            phaseStats.EnsureCreateRouteStateMs +
            phaseStats.EnsureUpdateTargetMs +
            phaseStats.EnsureFallbackMs;

        double ensureTrackedMs =
            Math.Max(
                preRouteOrInitialMs,
                Math.Max(
                    ensureInnerTrackedMs,
                    ensureUpdateTargetInnerMs));

        double tickOuterTrackedMs =
phaseStats.TickInitialDebugLogMs +
phaseStats.TickTargetPositionInitMs +
phaseStats.TickEnsureOuterMs +
phaseStats.TickRouteLookupAndRegisterMs +
phaseStats.TickStartTurnOuterMs +
phaseStats.TickRouteMoveOuterMs +
phaseStats.TickPublishOuterMs +
phaseStats.TickArrivalOrCompleteOuterMs;

        double trackedMs =
        Math.Max(
            tickOuterTrackedMs,
            ensureTrackedMs +
            phaseStats.RouteLookupMs +
            phaseStats.StartTurnMs +
            phaseStats.ArrivalThresholdMs +
            phaseStats.PathLengthMs +
            phaseStats.DistanceMathMs +
            phaseStats.PointOnPathMs +
            phaseStats.DirectionOnPathMs +
            phaseStats.ApplyStateMs +
            phaseStats.PublishMs +
            phaseStats.ArrivalCheckMs +
            phaseStats.CompleteMs +
            phaseStats.ClearRouteMs);

        double untrackedMs =
            Math.Max(0d, npcMs - trackedMs);

        double ensureTrackingGapMs =
            Math.Max(
                0d,
                ensureTrackedMs - Math.Max(ensureInnerTrackedMs, ensureUpdateTargetInnerMs));

        SystemNpcMovementTargetResolveStats resolveStats =
            phaseStats.TargetResolveStats;

        return " | TickPhaseStatsAvailable=True" +
               " | TickTrackedMs=" + trackedMs.ToString("F2") +
               " | TickUntrackedMs=" + untrackedMs.ToString("F2") +
               " | TickDominantPhase=" + phaseStats.GetDominantPhaseName() +
               " | TickDominantPhaseMs=" + phaseStats.GetDominantPhaseMs().ToString("F2") +
               " | TickPreRouteOrInitialMs=" + preRouteOrInitialMs.ToString("F2") +
               " | TickWaitingInitialRouteMs=" + phaseStats.TickWaitingInitialRouteMs.ToString("F2") +
               " | TickEnsureDirectionMs=" + phaseStats.EnsureDirectionMs.ToString("F2") +
               " | TickEnsureTrackedMs=" + ensureTrackedMs.ToString("F2") +
               " | TickEnsureInnerTrackedMs=" + ensureInnerTrackedMs.ToString("F2") +
               " | TickEnsureUpdateTargetInnerMs=" + ensureUpdateTargetInnerMs.ToString("F2") +
               " | TickEnsureTrackingGapMs=" + ensureTrackingGapMs.ToString("F2") +
               " | TickOuterTrackedMs=" + tickOuterTrackedMs.ToString("F2") +
                " | TickInitialDebugLogMs=" + phaseStats.TickInitialDebugLogMs.ToString("F2") +
                " | TickTargetPositionInitMs=" + phaseStats.TickTargetPositionInitMs.ToString("F2") +
                " | TickEnsureOuterMs=" + phaseStats.TickEnsureOuterMs.ToString("F2") +
                " | TickRouteLookupAndRegisterMs=" + phaseStats.TickRouteLookupAndRegisterMs.ToString("F2") +
                " | TickStartTurnOuterMs=" + phaseStats.TickStartTurnOuterMs.ToString("F2") +
                " | TickRouteMoveOuterMs=" + phaseStats.TickRouteMoveOuterMs.ToString("F2") +
                " | TickPublishOuterMs=" + phaseStats.TickPublishOuterMs.ToString("F2") +
                " | TickArrivalOrCompleteOuterMs=" + phaseStats.TickArrivalOrCompleteOuterMs.ToString("F2") +
                " | TickEnsureFallbackMs=" + phaseStats.EnsureFallbackMs.ToString("F2") +
                " | TickEnsureFallbackRerollTotalMs=" + phaseStats.EnsureFallbackRerollTotalMs.ToString("F2") +
                " | TickEnsureFallbackRerollDispatchMs=" + phaseStats.EnsureFallbackRerollDispatchMs.ToString("F2") +
                " | TickEnsureFallbackRerollClearRouteMs=" + phaseStats.EnsureFallbackRerollClearRouteMs.ToString("F2") +
                " | TickEnsureFallbackRerollResetStateMs=" + phaseStats.EnsureFallbackRerollResetStateMs.ToString("F2") +
                " | TickEnsureFallbackRerollLogMs=" + phaseStats.EnsureFallbackRerollLogMs.ToString("F2") +
                " | TickEnsureFallbackRerolled=" + phaseStats.EnsureFallbackRerolled +
                " | TickEnsureFallbackRerollKind=" + (phaseStats.EnsureFallbackRerollKind ?? string.Empty) +
                " | TickEnsureFallbackSystemExitConfigMs=" + phaseStats.EnsureFallbackSystemExitConfigMs.ToString("F2") +
                " | TickEnsureFallbackSystemExitRouteServiceMs=" + phaseStats.EnsureFallbackSystemExitRouteServiceMs.ToString("F2") +
                " | TickEnsureFallbackSystemExitSetupMs=" + phaseStats.EnsureFallbackSystemExitSetupMs.ToString("F2") +
                " | TickEnsureFallbackSystemExitLoopMs=" + phaseStats.EnsureFallbackSystemExitLoopMs.ToString("F2") +
                " | TickEnsureFallbackSystemExitInvalidPointCheckMs=" + phaseStats.EnsureFallbackSystemExitInvalidPointCheckMs.ToString("F2") +
                " | TickEnsureFallbackSystemExitAssignMs=" + phaseStats.EnsureFallbackSystemExitAssignMs.ToString("F2") +
                " | TickEnsureFallbackSystemExitRouteCount=" + phaseStats.EnsureFallbackSystemExitRouteCount +
                " | TickEnsureFallbackSystemExitAttemptsChecked=" + phaseStats.EnsureFallbackSystemExitAttemptsChecked +
                " | TickEnsureFallbackSystemExitUsedFallback=" + phaseStats.EnsureFallbackSystemExitUsedFallback +
                " | TickEnsureFallbackMapConfigMs=" + phaseStats.EnsureFallbackMapConfigMs.ToString("F2") +
                " | TickEnsureFallbackMapPatrolBoundsMs=" + phaseStats.EnsureFallbackMapPatrolBoundsMs.ToString("F2") +
                " | TickEnsureFallbackMapSetupMs=" + phaseStats.EnsureFallbackMapSetupMs.ToString("F2") +
                " | TickEnsureFallbackMapLoopMs=" + phaseStats.EnsureFallbackMapLoopMs.ToString("F2") +
                " | TickEnsureFallbackMapRandomPositionMs=" + phaseStats.EnsureFallbackMapRandomPositionMs.ToString("F2") +
                " | TickEnsureFallbackMapInvalidPointCheckMs=" + phaseStats.EnsureFallbackMapInvalidPointCheckMs.ToString("F2") +
                " | TickEnsureFallbackMapAssignMs=" + phaseStats.EnsureFallbackMapAssignMs.ToString("F2") +
                " | TickEnsureFallbackMapAttemptsConfigured=" + phaseStats.EnsureFallbackMapAttemptsConfigured +
                " | TickEnsureFallbackMapAttemptsChecked=" + phaseStats.EnsureFallbackMapAttemptsChecked +
                " | TickEnsureFallbackMapUsedFallback=" + phaseStats.EnsureFallbackMapUsedFallback +
               " | TickEnsureFastRouteTotalMs=" + phaseStats.EnsureFastRouteTotalMs.ToString("F2") +
               " | TickEnsureFastRouteLookupMs=" + phaseStats.EnsureFastRouteLookupMs.ToString("F2") +
               " | TickEnsureFastRouteReusableCheckMs=" + phaseStats.EnsureFastRouteReusableCheckMs.ToString("F2") +
               " | TickEnsureFastRouteUpdateTargetMs=" + phaseStats.EnsureFastRouteUpdateTargetMs.ToString("F2") +
               " | TickEnsureFastRouteArrivalMathMs=" + phaseStats.EnsureFastRouteArrivalMathMs.ToString("F2") +
               " | TickEnsureFastRouteLogMs=" + phaseStats.EnsureFastRouteLogMs.ToString("F2") +
               " | TickEnsureFastRoutePathPointCount=" + phaseStats.EnsureFastRoutePathPointCount +
               " | TickEnsureUpdateTargetPrecheckMs=" + phaseStats.EnsureUpdateTargetPrecheckMs.ToString("F2") +
               " | TickEnsureUpdateTargetStartTurnMs=" + phaseStats.EnsureUpdateTargetStartTurnMs.ToString("F2") +
               " | TickEnsureUpdateTargetPathLengthMs=" + phaseStats.EnsureUpdateTargetPathLengthMs.ToString("F2") +
               " | TickEnsureUpdateTargetArrivalThresholdMs=" + phaseStats.EnsureUpdateTargetArrivalThresholdMs.ToString("F2") +
               " | TickEnsureUpdateTargetDistanceMathMs=" + phaseStats.EnsureUpdateTargetDistanceMathMs.ToString("F2") +
               " | TickEnsureUpdateTargetPointOnPathMs=" + phaseStats.EnsureUpdateTargetPointOnPathMs.ToString("F2") +
               " | TickEnsureUpdateTargetAssignMs=" + phaseStats.EnsureUpdateTargetAssignMs.ToString("F2") +
               " | TickRouteLookupMs=" + phaseStats.RouteLookupMs.ToString("F2") +
               " | TickStartTurnMs=" + phaseStats.StartTurnMs.ToString("F2") +
               " | TickArrivalThresholdMs=" + phaseStats.ArrivalThresholdMs.ToString("F2") +
               " | TickPathLengthMs=" + phaseStats.PathLengthMs.ToString("F2") +
               " | TickDistanceMathMs=" + phaseStats.DistanceMathMs.ToString("F2") +
               " | TickPointOnPathMs=" + phaseStats.PointOnPathMs.ToString("F2") +
               " | TickDirectionOnPathMs=" + phaseStats.DirectionOnPathMs.ToString("F2") +
               " | TickApplyStateMs=" + phaseStats.ApplyStateMs.ToString("F2") +
               " | TickPublishMs=" + phaseStats.PublishMs.ToString("F2") +
               " | TickArrivalCheckMs=" + phaseStats.ArrivalCheckMs.ToString("F2") +
               " | TickCompleteMs=" + phaseStats.CompleteMs.ToString("F2") +
               " | TickCompleteTotalMs=" + phaseStats.CompleteTotalMs.ToString("F2") +
               " | TickCompleteWrapperTotalMs=" + phaseStats.CompleteWrapperTotalMs.ToString("F2") +
                " | TickCompleteDelayCheckMs=" + phaseStats.CompleteDelayCheckMs.ToString("F2") +
                " | TickCompleteDelayCurrentSystemCheckMs=" + phaseStats.CompleteDelayCurrentSystemCheckMs.ToString("F2") +
                " | TickCompleteDelayTravelStateCheckMs=" + phaseStats.CompleteDelayTravelStateCheckMs.ToString("F2") +
                " | TickCompleteDelayRouteTargetKindMs=" + phaseStats.CompleteDelayRouteTargetKindMs.ToString("F2") +
                " | TickCompleteDelayCounterMs=" + phaseStats.CompleteDelayCounterMs.ToString("F2") +
                " | TickCompleteMovementBodyMs=" + phaseStats.CompleteMovementBodyMs.ToString("F2") +
                " | TickCompleteDelayWasApplied=" + phaseStats.CompleteDelayWasApplied +
                " | TickCompleteDelayDecision=" + (phaseStats.CompleteDelayDecision ?? string.Empty) +
                " | TickCompleteDelayCompletionsUsedBefore=" + phaseStats.CompleteDelayCompletionsUsedBefore +
                " | TickCompleteDelayCompletionsUsedAfter=" + phaseStats.CompleteDelayCompletionsUsedAfter +
                " | TickCompleteDelayMaxCompletionsPerTick=" + phaseStats.CompleteDelayMaxCompletionsPerTick +
               " | TickCompletePrecheckMs=" + phaseStats.CompletePrecheckMs.ToString("F2") +
               " | TickCompleteClearRouteMs=" + phaseStats.CompleteClearRouteMs.ToString("F2") +
               " | TickCompleteAssignPositionMs=" + phaseStats.CompleteAssignPositionMs.ToString("F2") +
               " | TickCompleteStateSwitchMs=" + phaseStats.CompleteStateSwitchMs.ToString("F2") +
               " | TickCompleteSystemTravelMs=" + phaseStats.CompleteSystemTravelMs.ToString("F2") +
               " | TickCompleteLogMs=" + phaseStats.CompleteLogMs.ToString("F2") +
               " | TickCompletePublishEventMs=" + phaseStats.CompletePublishEventMs.ToString("F2") +
               " | TickCompleteBehaviorMs=" + phaseStats.CompleteBehaviorMs.ToString("F2") +
               " | TickCompleteWasBlocked=" + phaseStats.CompleteWasBlocked +
               " | TickCompleteWasSystemTravel=" + phaseStats.CompleteWasSystemTravel +
               " | TickCompleteRouteTargetKind=" + (phaseStats.CompleteRouteTargetKind ?? string.Empty) +
               " | TickCompleteRouteTargetMoveKind=" + (phaseStats.CompleteRouteTargetMoveKind ?? string.Empty) +
               " | TickCompleteBehaviorBefore=" + (phaseStats.CompleteBehaviorBefore ?? string.Empty) +
               " | TickCompleteTravelStateBefore=" + (phaseStats.CompleteTravelStateBefore ?? string.Empty) +
               " | TickCompleteTravelStateAfter=" + (phaseStats.CompleteTravelStateAfter ?? string.Empty) +
               " | TickCompleteSystemBefore=" + (phaseStats.CompleteSystemBefore ?? string.Empty) +
               " | TickCompleteSystemAfter=" + (phaseStats.CompleteSystemAfter ?? string.Empty) +
               " | TickCompleteTargetSystemId=" + (phaseStats.CompleteTargetSystemId ?? string.Empty) +
               " | TickCompleteTargetPlanetId=" + (phaseStats.CompleteTargetPlanetId ?? string.Empty) +
               " | TickCompleteCurrentPlanetBefore=" + (phaseStats.CompleteCurrentPlanetBefore ?? string.Empty) +
               " | TickCompleteCurrentPlanetAfter=" + (phaseStats.CompleteCurrentPlanetAfter ?? string.Empty) +
               " | TickClearRouteMs=" + phaseStats.ClearRouteMs.ToString("F2") +
               " | TickRouteMissing=" + phaseStats.RouteMissingCount +
               " | TickArrivedAfterEnsureDirection=" + phaseStats.ArrivedAfterEnsureDirectionCount +
               " | TickStartTurnConsumed=" + phaseStats.StartTurnConsumedCount +
               " | TickCompleted=" + phaseStats.CompletedCount +
               " | TickClearedRoute=" + phaseStats.ClearedRouteCount +
               " | TickPublishedPositionChanged=" + phaseStats.PublishedPositionChangedCount +
               " | TickPathPointCountTotal=" + phaseStats.PathPointCountTotal +
               " | TickMaxPathPointCount=" + phaseStats.MaxPathPointCount +
               " | TickEnsureDecision=" + phaseStats.EnsureDecision +
               " | TickResolveGetNextTargetMs=" + resolveStats.GetNextTargetMs.ToString("F2") +
               " | TickResolveGetNextTargetAllyMs=" + resolveStats.GetNextTargetAllyMs.ToString("F2") +
               " | TickResolveSystemExitCheckMs=" + resolveStats.SystemExitCheckMs.ToString("F2") +
               " | TickResolveSystemExitLogMs=" + resolveStats.SystemExitLogMs.ToString("F2") +
               " | TickResolveSystemExitInvalidPointCheckMs=" + resolveStats.SystemExitInvalidPointCheckMs.ToString("F2") +
               " | TickResolveSystemExitReturnMs=" + resolveStats.SystemExitReturnMs.ToString("F2") +
               " | TickResolveSystemExitResetMs=" + resolveStats.SystemExitResetMs.ToString("F2") +
               " | TickResolveSystemExitCheckCount=" + resolveStats.SystemExitCheckCount +
               " | TickResolveSystemExitLogCount=" + resolveStats.SystemExitLogCount +
               " | TickResolveSystemExitInvalidPointCheckCount=" + resolveStats.SystemExitInvalidPointCheckCount +
               " | TickResolveSystemExitReturnCount=" + resolveStats.SystemExitReturnCount +
               " | TickResolveSystemExitResetCount=" + resolveStats.SystemExitResetCount;
    }

    private bool ShouldLogNpcPerf(double elapsedMs)
    {
        if (elapsedMs < PerfLogThresholdMs)
            return false;

        return IsNpcMovementPerformanceLogEnabled();
    }

    private DebugLogConfig GetDebugLogConfig()
    {
        return Bootstrapper.Instance != null
            ? Bootstrapper.Instance.DebugLogConfig
            : null;
    }

    private bool ShouldLogNpcBehaviorActionAnalytics(
        DebugLogConfig debugLogConfig,
        int currentTick,
        float approxFps,
        double totalMs,
        out bool includeDetailedActionFields)
    {
        includeDetailedActionFields = false;

        if (debugLogConfig == null)
            return false;

        if (!debugLogConfig.NpcBehaviorActionAnalyticsLogs)
            return false;

        NpcMovementLoadAnalyticsDetailLevel detailLevel =
            debugLogConfig.NpcMovementLoadAnalyticsDetailLevel;

        if (detailLevel == NpcMovementLoadAnalyticsDetailLevel.Off)
            return false;

        bool fpsWarning =
            debugLogConfig.NpcBehaviorActionAnalyticsOnFpsWarning &&
            approxFps > 0f &&
            approxFps <= debugLogConfig.NpcMovementLoadAnalyticsFpsWarningThreshold;

        bool tickWarning =
            totalMs >= debugLogConfig.NpcBehaviorActionAnalyticsTickWarningMs;

        bool regularSample =
            debugLogConfig.NpcBehaviorActionAnalyticsRegularLogIntervalTicks > 0 &&
            currentTick % debugLogConfig.NpcBehaviorActionAnalyticsRegularLogIntervalTicks == 0;

        switch (detailLevel)
        {
            case NpcMovementLoadAnalyticsDetailLevel.SummaryOnly:
                includeDetailedActionFields = false;
                return fpsWarning || tickWarning || regularSample;

            case NpcMovementLoadAnalyticsDetailLevel.DetailedOnSpike:
                includeDetailedActionFields = fpsWarning || tickWarning;
                return includeDetailedActionFields;

            case NpcMovementLoadAnalyticsDetailLevel.DetailedRegularAndSpike:
                includeDetailedActionFields = fpsWarning || tickWarning || regularSample;
                return includeDetailedActionFields;

            case NpcMovementLoadAnalyticsDetailLevel.DetailedEveryTick:
                includeDetailedActionFields = true;
                return true;

            default:
                includeDetailedActionFields = false;
                return false;
        }
    }

    private bool IsNpcMilitaryMovementVerboseDebugEnabled()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.NpcMilitaryMovementVerboseLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.NpcMovement);
    }

    private bool WasLastNpcRouteBuildRejectedByTimeBudget()
    {
        return _npcRouteBuildResult != null &&
               !string.IsNullOrWhiteSpace(_npcRouteBuildResult.LastRejectReason) &&
               _npcRouteBuildResult.LastRejectReason.StartsWith(
                   "TimeBudgetExceeded:",
                   StringComparison.Ordinal);
    }

    private IReadOnlyList<SystemNpcRuntimeState> PrioritizeNpcMovementOrder(
        IReadOnlyList<SystemNpcRuntimeState> npcs)
    {
        _npcMovementPriorityBuffer.Clear();
        _npcMovementPriorityDistanceBuffer.Clear();
        _npcMovementRegularBuffer.Clear();
        _npcMovementPrioritySet.Clear();

        if (npcs == null ||
            npcs.Count == 0)
        {
            return _npcMovementPriorityBuffer;
        }

        int maxPriorityRouteBuildsPerTick =
            GetPriorityFinalApproachRouteBuildsPerTick();

        if (maxPriorityRouteBuildsPerTick > 0)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                SystemNpcRuntimeState npc = npcs[i];

                if (!TryGetNpcMovementPriorityDistanceSqr(npc, out float distanceSqr))
                    continue;

                InsertPriorityNpcByDistance(
                    npc,
                    distanceSqr,
                    maxPriorityRouteBuildsPerTick);
            }
        }

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc != null &&
                _npcMovementPrioritySet.Contains(npc))
            {
                continue;
            }

            _npcMovementRegularBuffer.Add(npc);
        }

        for (int i = 0; i < _npcMovementRegularBuffer.Count; i++)
            _npcMovementPriorityBuffer.Add(_npcMovementRegularBuffer[i]);

        _npcMovementPriorityDistanceBuffer.Clear();
        _npcMovementRegularBuffer.Clear();
        _npcMovementPrioritySet.Clear();

        return _npcMovementPriorityBuffer;
    }

    private bool TryGetNpcMovementPriorityDistanceSqr(
        SystemNpcRuntimeState npc,
        out float distanceSqr)
    {
        distanceSqr = float.MaxValue;

        if (npc == null)
            return false;

        bool isPriorityCandidate =
            IsNpcFinalPlanetApproachRoutePriorityCandidate(npc) ||
            IsNpcEnemyApproachRoutePriorityCandidate(npc);

        if (!isPriorityCandidate)
            return false;

        Vector3 targetPosition =
            _routeService != null
                ? _routeService.GetNextTargetPosition(npc)
                : npc.TargetPosition;

        targetPosition.z = -2f;

        if (!IsFinite(targetPosition))
            return false;

        distanceSqr =
            (targetPosition - npc.CurrentPosition).sqrMagnitude;

        return true;
    }

    private bool IsNpcFinalPlanetApproachRoutePriorityCandidate(
    SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (npc.CurrentBehavior != SystemNpcBehaviorType.PlanetToPlanetTravel)
            return false;

        if (npc.TravelState != SystemNpcTravelState.TravelingInsideSystem)
            return false;

        if (string.IsNullOrWhiteSpace(npc.TargetPlanetId))
            return false;

        if (!DoesNpcNeedRouteBuildSoon(npc))
            return false;

        Vector3 targetPosition =
            _routeService != null
                ? _routeService.GetNextTargetPosition(npc)
                : npc.TargetPosition;

        targetPosition.z = -2f;

        if (!IsFinite(targetPosition))
            return false;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                targetPosition);

        float distancePerTick =
            Mathf.Max(0f, npc.Speed) *
            GetSpeedMultiplier();

        float priorityDistance =
            arrivalThreshold +
            distancePerTick * GetFinalPlanetApproachRefreshTicks();

        float distanceToTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                targetPosition);

        return distanceToTarget <= priorityDistance;
    }

    private bool IsNpcEnemyApproachRoutePriorityCandidate(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (npc.TravelState != SystemNpcTravelState.EngagingEnemy &&
            string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
        {
            return false;
        }

        return DoesNpcNeedRouteBuildSoon(npc);
    }

    private void InsertPriorityNpcByDistance(
        SystemNpcRuntimeState npc,
        float distanceSqr,
        int maxPriorityCount)
    {
        if (npc == null ||
            maxPriorityCount <= 0)
        {
            return;
        }

        int insertIndex = 0;

        while (insertIndex < _npcMovementPriorityDistanceBuffer.Count &&
               _npcMovementPriorityDistanceBuffer[insertIndex] <= distanceSqr)
        {
            insertIndex++;
        }

        if (_npcMovementPriorityBuffer.Count >= maxPriorityCount &&
            insertIndex >= maxPriorityCount)
        {
            return;
        }

        _npcMovementPriorityBuffer.Insert(insertIndex, npc);
        _npcMovementPriorityDistanceBuffer.Insert(insertIndex, distanceSqr);
        _npcMovementPrioritySet.Add(npc);

        if (_npcMovementPriorityBuffer.Count <= maxPriorityCount)
            return;

        int lastIndex =
            _npcMovementPriorityBuffer.Count - 1;

        SystemNpcRuntimeState removedNpc =
            _npcMovementPriorityBuffer[lastIndex];

        _npcMovementPriorityBuffer.RemoveAt(lastIndex);
        _npcMovementPriorityDistanceBuffer.RemoveAt(lastIndex);

        if (removedNpc != null)
            _npcMovementPrioritySet.Remove(removedNpc);
    }

    private bool DoesNpcNeedRouteBuildSoon(
        SystemNpcRuntimeState npc)
    {
        if (npc == null ||
            string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
        {
            return false;
        }

        if (npc.IsWaitingForInitialRouteBuild)
            return true;

        if (!_npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1 ||
            !IsSameNpcMovementRouteContext(routeState, npc))
        {
            return true;
        }

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        if (totalRouteLength <= 0f)
            return true;

        float distancePerTick =
            Mathf.Max(0f, npc.Speed) *
            GetSpeedMultiplier();

        float remainingRouteDistance =
            Mathf.Max(
                0f,
                totalRouteLength - routeState.DistanceTravelled);

        return remainingRouteDistance <=
               distancePerTick * GetFinalPlanetApproachRefreshTicks();
    }

    private int GetFinalPlanetApproachRefreshTicks()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return 3;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .FinalPlanetApproachRefreshTicks;
    }

    private int GetPriorityFinalApproachRouteBuildsPerTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.CurrentSystemNpcSimulationConfig == null)
        {
            return DefaultPriorityFinalApproachRouteBuildsPerTick;
        }

        return Bootstrapper
            .Instance
            .CurrentSystemNpcSimulationConfig
            .PriorityFinalApproachRouteBuildsPerTick;
    }

    private bool IsNpcPlanetApproachQueueCoordinateLogEnabled()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.NpcPlanetApproachQueueCoordinateLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.NpcMovement);
    }

    private void LogNpcPlanetApproachQueueCoordinates(
        string decision,
        SystemNpcRuntimeState npc,
        int currentTick,
        NpcMovementRouteState routeState)
    {
        if (!IsNpcPlanetApproachQueueCoordinateLogEnabled())
            return;

        if (!ShouldLogNpcPlanetApproachQueueCoordinates(npc))
            return;

        NpcMovementRouteState activeRouteState = routeState;

        if (activeRouteState == null &&
            npc != null &&
            !string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
        {
            _npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out activeRouteState);
        }

        Vector3 rawTargetPosition =
            _routeService != null
                ? _routeService.GetNextTargetPosition(npc)
                : npc.TargetPosition;

        rawTargetPosition.z = -2f;

        Vector3 currentTargetPosition =
            npc.TargetPosition != Vector3.zero
                ? npc.TargetPosition
                : rawTargetPosition;

        currentTargetPosition.z = -2f;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                currentTargetPosition);

        float distanceToCurrentTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                currentTargetPosition);

        float distanceToRawTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                rawTargetPosition);

        float distancePerTick =
            Mathf.Max(0f, npc.Speed) *
            GetSpeedMultiplier();

        int routePathCount =
            activeRouteState != null && activeRouteState.Path != null
                ? activeRouteState.Path.Count
                : 0;

        float routeLength =
            activeRouteState != null && activeRouteState.Path != null
                ? GetNpcPathLength(activeRouteState.Path)
                : 0f;

        float routeDistanceTravelled =
            activeRouteState != null
                ? activeRouteState.DistanceTravelled
                : 0f;

        float remainingRouteDistance =
            Mathf.Max(
                0f,
                routeLength - routeDistanceTravelled);

        string lastRejectReason =
            _npcRouteBuildResult != null &&
            !string.IsNullOrWhiteSpace(_npcRouteBuildResult.LastRejectReason)
                ? _npcRouteBuildResult.LastRejectReason
                : "None";

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.NpcMovement,
            "[SystemNpcMovementService] NPC_PLANET_APPROACH_QUEUE" +
            " | Decision=" + decision +
            " | Scope=" + GetPerfScope(npc.CurrentSystemId) +
            " | CurrentSystemId=" + GetCurrentSystemIdForPerf() +
            " | NpcSystemId=" + npc.CurrentSystemId +
            " | Npc=" + npc.RuntimeNpcId +
            " | Behavior=" + npc.CurrentBehavior +
            " | TravelState=" + npc.TravelState +
            " | TargetPlanetId=" + npc.TargetPlanetId +
            " | CurrentPlanetId=" + npc.CurrentPlanetId +
            " | IsOnPlanet=" + npc.IsOnPlanet +
            " | IsWaitingForInitialRouteBuild=" + npc.IsWaitingForInitialRouteBuild +
            " | CurrentPosition=" + FormatVector3(npc.CurrentPosition) +
            " | TargetPosition=" + FormatVector3(npc.TargetPosition) +
            " | RawTargetPosition=" + FormatVector3(rawTargetPosition) +
            " | CurrentTargetPosition=" + FormatVector3(currentTargetPosition) +
            " | DistanceToCurrentTarget=" + distanceToCurrentTarget.ToString("0.###") +
            " | DistanceToRawTarget=" + distanceToRawTarget.ToString("0.###") +
            " | ArrivalThreshold=" + arrivalThreshold.ToString("0.###") +
            " | Speed=" + npc.Speed.ToString("0.###") +
            " | DistancePerTick=" + distancePerTick.ToString("0.###") +
            " | HasRouteState=" + (activeRouteState != null) +
            " | RoutePathCount=" + routePathCount +
            " | RouteLength=" + routeLength.ToString("0.###") +
            " | RouteDistanceTravelled=" + routeDistanceTravelled.ToString("0.###") +
            " | RemainingRouteDistance=" + remainingRouteDistance.ToString("0.###") +
            " | RouteDestination=" + (activeRouteState != null ? FormatVector3(activeRouteState.Destination) : "None") +
            " | RouteBuildTick=" + (activeRouteState != null ? activeRouteState.BuildTick.ToString() : "None") +
            " | RouteTick=" + (activeRouteState != null ? activeRouteState.Tick.ToString() : "None") +
            " | InitialRouteBuildsUsedThisTick=" + _initialRouteBuildsUsedThisTick +
            " | MaxInitialRouteBuildsPerTick=" + GetMaxInitialRouteBuildsPerTick() +
            " | LastRouteBuildRejectReason=" + lastRejectReason +
            " | Tick=" + currentTick);
    }

    private bool ShouldLogNpcPlanetApproachQueueCoordinates(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (npc.CurrentBehavior != SystemNpcBehaviorType.PlanetToPlanetTravel)
            return false;

        if (npc.TravelState != SystemNpcTravelState.TravelingInsideSystem)
            return false;

        if (string.IsNullOrWhiteSpace(npc.TargetPlanetId))
            return false;

        return true;
    }

    private bool IsNpcMovementPerformanceLogEnabled()
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.NpcMovement);
    }

    private void LogCurrentSystemNpcMovementDetailIfNeeded(
    int currentTick,
    float deltaTime,
    double totalMs,
    double getNpcsMs,
    double getAliveNpcsMs,
    double prioritizeNpcsMs,
    double loopMs,
    string scope,
    string currentSystemId,
    string tickSystemId,
    int npcCount,
    int movedCount,
    int blockedCount,
    int fixedPatrolPointCount,
    int fixedSystemExitPointCount,
    int movingPlanetCount,
    int movingNpcOrEnemyCount,
    int fixedMapPointCount,
    int unknownRouteTargetCount,
    double fixedPatrolPointMs,
    double fixedSystemExitPointMs,
    double movingPlanetMs,
    double movingNpcOrEnemyMs,
    double fixedMapPointMs,
    double unknownRouteTargetMs,
    string dominantRouteTargetKind,
    double dominantRouteTargetMs,
    double maxNpcMs,
    string maxNpcId,
    string maxNpcRouteTargetKind,
    string maxNpcRouteTargetMoveKind,
    NpcDetailedMovementPhaseStats phaseStats)
    {
        if (!string.Equals(scope, "CURRENT_SYSTEM", StringComparison.Ordinal))
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        DebugLogConfig debugLogConfig = GetDebugLogConfig();

        if (debugLogConfig == null)
            return;

        double thresholdMs =
            Math.Max(
                1.0,
                debugLogConfig.NpcMovementLoadAnalyticsTickWarningMs);

        if (totalMs < thresholdMs)
            return;

        double frameMs =
            deltaTime > 0f
                ? deltaTime * 1000.0
                : 0.0;

        double approxFps =
            deltaTime > 0f
                ? 1.0 / deltaTime
                : 0.0;

        double resolveMovementTargetMs = 0d;
        double buildRouteMs = 0d;
        double refreshRouteMs = 0d;
        double reuseOldRouteMs = 0d;
        double moveAlongRouteMs = 0d;
        double completeMovementMs = 0d;
        double waitingInitialRouteMs = 0d;
        double ensureDirectionMs = 0d;
        double ensureInnerTrackedMs = 0d;
        double ensureDirectionUntrackedMs = 0d;
        double untrackedMs = 0d;

        string dominantPhase = string.Empty;
        double dominantPhaseMs = 0d;

        int routeMissingCount = 0;
        int completedCount = 0;
        int publishedPositionChangedCount = 0;
        int pathPointCountTotal = 0;
        int maxPathPointCount = 0;
        int startTurnConsumedCount = 0;

        string ensureDecision = string.Empty;

        if (phaseStats != null)
        {
            ensureDirectionMs =
                phaseStats.EnsureDirectionMs;

            resolveMovementTargetMs =
                phaseStats.EnsureResolveTargetMs +
                phaseStats.EnsureBoundaryMs +
                phaseStats.EnsureArrivalBeforeBuildMs +
                phaseStats.EnsureActiveRouteLookupMs;

            buildRouteMs =
                phaseStats.EnsureBuildRouteMs;

            refreshRouteMs =
                phaseStats.EnsureFastRouteTotalMs +
                phaseStats.EnsureUpdateTargetMs;

            reuseOldRouteMs =
                phaseStats.EnsureReuseChecksMs;

            moveAlongRouteMs =
                phaseStats.RouteLookupMs +
                phaseStats.StartTurnMs +
                phaseStats.ArrivalThresholdMs +
                phaseStats.PathLengthMs +
                phaseStats.DistanceMathMs +
                phaseStats.PointOnPathMs +
                phaseStats.DirectionOnPathMs +
                phaseStats.ApplyStateMs +
                phaseStats.PublishMs +
                phaseStats.ArrivalCheckMs +
                phaseStats.ClearRouteMs;

            completeMovementMs =
                phaseStats.CompleteMs;

            waitingInitialRouteMs =
                phaseStats.TickWaitingInitialRouteMs;

            ensureInnerTrackedMs =
                phaseStats.EnsureEntrySnapshotMs +
                phaseStats.EnsureFastRouteTotalMs +
                phaseStats.EnsureResolveTargetMs +
                phaseStats.EnsureBoundaryMs +
                phaseStats.EnsureArrivalMathMs +
                phaseStats.EnsureArrivalBeforeBuildMs +
                phaseStats.EnsureActiveRouteLookupMs +
                phaseStats.EnsureReuseChecksMs +
                phaseStats.EnsureRouteReuseDistanceMs +
                phaseStats.EnsureInitialLimitMs +
                phaseStats.EnsureBudgetCheckMs +
                phaseStats.EnsureBudgetEnterMs +
                phaseStats.EnsureBuildRouteMs +
                phaseStats.EnsureCreateRouteStateMs +
                phaseStats.EnsureUpdateTargetMs +
                phaseStats.EnsureFallbackMs;

            ensureDirectionUntrackedMs =
                Math.Max(0d, ensureDirectionMs - ensureInnerTrackedMs);

            dominantPhase =
                phaseStats.GetDominantPhaseName();

            dominantPhaseMs =
                phaseStats.GetDominantPhaseMs();

            routeMissingCount =
                phaseStats.RouteMissingCount;

            completedCount =
                phaseStats.CompletedCount;

            publishedPositionChangedCount =
                phaseStats.PublishedPositionChangedCount;

            pathPointCountTotal =
                phaseStats.PathPointCountTotal;

            maxPathPointCount =
                phaseStats.MaxPathPointCount;

            startTurnConsumedCount =
                phaseStats.StartTurnConsumedCount;

            ensureDecision =
                phaseStats.EnsureDecision ?? string.Empty;

            double trackedMs =
                getNpcsMs +
                loopMs;

            untrackedMs =
                Math.Max(0d, totalMs - trackedMs);
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[CURRENT_SYSTEM_NPC_MOVEMENT_DETAIL]" +
            " UnityFrame=" + Time.frameCount +
            " | Tick=" + currentTick +
            " | CurrentSystemId=" + currentSystemId +
            " | TickSystemId=" + tickSystemId +
            " | FrameMs=" + frameMs.ToString("F2") +
            " | ApproxFps=" + approxFps.ToString("F1") +
            " | TotalMs=" + totalMs.ToString("F2") +
            " | GetNpcsMs=" + getNpcsMs.ToString("F2") +
            " | GetAliveNpcsMs=" + getAliveNpcsMs.ToString("F2") +
            " | PrioritizeNpcsMs=" + prioritizeNpcsMs.ToString("F2") +
            " | LoopMs=" + loopMs.ToString("F2") +
            " | Npcs=" + npcCount +
            " | Moved=" + movedCount +
            " | Blocked=" + blockedCount +
            " | FixedPatrolPointCount=" + fixedPatrolPointCount +
            " | FixedSystemExitPointCount=" + fixedSystemExitPointCount +
            " | MovingPlanetCount=" + movingPlanetCount +
            " | MovingNpcOrEnemyCount=" + movingNpcOrEnemyCount +
            " | FixedMapPointCount=" + fixedMapPointCount +
            " | UnknownRouteTargetCount=" + unknownRouteTargetCount +
            " | FixedPatrolPointMs=" + fixedPatrolPointMs.ToString("F2") +
            " | FixedSystemExitPointMs=" + fixedSystemExitPointMs.ToString("F2") +
            " | MovingPlanetMs=" + movingPlanetMs.ToString("F2") +
            " | MovingNpcOrEnemyMs=" + movingNpcOrEnemyMs.ToString("F2") +
            " | FixedMapPointMs=" + fixedMapPointMs.ToString("F2") +
            " | UnknownRouteTargetMs=" + unknownRouteTargetMs.ToString("F2") +
            " | DominantRouteTargetKind=" + (dominantRouteTargetKind ?? string.Empty) +
            " | DominantRouteTargetMs=" + dominantRouteTargetMs.ToString("F2") +
            " | ResolveMovementTargetMs=" + resolveMovementTargetMs.ToString("F2") +
            " | BuildRouteMs=" + buildRouteMs.ToString("F2") +
            " | RefreshRouteMs=" + refreshRouteMs.ToString("F2") +
            " | ReuseOldRouteMs=" + reuseOldRouteMs.ToString("F2") +
            " | MoveAlongRouteMs=" + moveAlongRouteMs.ToString("F2") +
            " | CompleteMovementMs=" + completeMovementMs.ToString("F2") +
            " | WaitingInitialRouteMs=" + waitingInitialRouteMs.ToString("F2") +
            " | EnsureDirectionMs=" + ensureDirectionMs.ToString("F2") +
            " | EnsureInnerTrackedMs=" + ensureInnerTrackedMs.ToString("F2") +
            " | EnsureDirectionUntrackedMs=" + ensureDirectionUntrackedMs.ToString("F2") +
            " | EnsureEntrySnapshotMs=" + (phaseStats != null ? phaseStats.EnsureEntrySnapshotMs.ToString("F2") : "0.00") +
            " | EnsureArrivalMathMs=" + (phaseStats != null ? phaseStats.EnsureArrivalMathMs.ToString("F2") : "0.00") +
            " | EnsureRouteReuseDistanceMs=" + (phaseStats != null ? phaseStats.EnsureRouteReuseDistanceMs.ToString("F2") : "0.00") +
            " | EnsureBudgetCheckMs=" + (phaseStats != null ? phaseStats.EnsureBudgetCheckMs.ToString("F2") : "0.00") +
            " | EnsureBudgetEnterMs=" + (phaseStats != null ? phaseStats.EnsureBudgetEnterMs.ToString("F2") : "0.00") +
            " | UntrackedMs=" + untrackedMs.ToString("F2") +
            " | DominantPhase=" + dominantPhase +
            " | DominantPhaseMs=" + dominantPhaseMs.ToString("F2") +
            " | MaxNpcMs=" + maxNpcMs.ToString("F2") +
            " | MaxNpc=" + (maxNpcId ?? string.Empty) +
            " | MaxNpcRouteTargetKind=" + (maxNpcRouteTargetKind ?? string.Empty) +
            " | MaxNpcRouteTargetMoveKind=" + (maxNpcRouteTargetMoveKind ?? string.Empty) +
            " | RouteMissing=" + routeMissingCount +
            " | Completed=" + completedCount +
            " | PublishedPositionChanged=" + publishedPositionChangedCount +
            " | StartTurnConsumed=" + startTurnConsumedCount +
            " | PathPointCountTotal=" + pathPointCountTotal +
            " | MaxPathPointCount=" + maxPathPointCount +
            " | EnsureDecision=" + ensureDecision +
            " | EnsureBuildRouteMs=" + (phaseStats != null ? phaseStats.EnsureBuildRouteMs.ToString("F2") : "0.00") +
            " | EnsureCreateRouteStateMs=" + (phaseStats != null ? phaseStats.EnsureCreateRouteStateMs.ToString("F2") : "0.00") +
            " | EnsureInitialLimitMs=" + (phaseStats != null ? phaseStats.EnsureInitialLimitMs.ToString("F2") : "0.00") +
            " | EnsureFastRouteTotalMs=" + (phaseStats != null ? phaseStats.EnsureFastRouteTotalMs.ToString("F2") : "0.00") +
            " | EnsureResolveTargetMs=" + (phaseStats != null ? phaseStats.EnsureResolveTargetMs.ToString("F2") : "0.00") +
            " | EnsureBoundaryMs=" + (phaseStats != null ? phaseStats.EnsureBoundaryMs.ToString("F2") : "0.00") +
            " | EnsureArrivalBeforeBuildMs=" + (phaseStats != null ? phaseStats.EnsureArrivalBeforeBuildMs.ToString("F2") : "0.00") +
            " | EnsureActiveRouteLookupMs=" + (phaseStats != null ? phaseStats.EnsureActiveRouteLookupMs.ToString("F2") : "0.00") +
            " | EnsureReuseChecksMs=" + (phaseStats != null ? phaseStats.EnsureReuseChecksMs.ToString("F2") : "0.00") +
            " | EnsureUpdateTargetMs=" + (phaseStats != null ? phaseStats.EnsureUpdateTargetMs.ToString("F2") : "0.00") +
            " | EnsureFallbackMs=" + (phaseStats != null ? phaseStats.EnsureFallbackMs.ToString("F2") : "0.00") +
            " | CompleteBehaviorMs=" + (phaseStats != null ? phaseStats.CompleteBehaviorMs.ToString("F2") : "0.00") +
            " | CompletePublishEventMs=" + (phaseStats != null ? phaseStats.CompletePublishEventMs.ToString("F2") : "0.00") +
            " | CompleteSystemTravelMs=" + (phaseStats != null ? phaseStats.CompleteSystemTravelMs.ToString("F2") : "0.00") +
            " | CompleteDelayWasApplied=" + (phaseStats != null && phaseStats.CompleteDelayWasApplied) +
            " | CompleteDelayDecision=" + (phaseStats != null ? phaseStats.CompleteDelayDecision : string.Empty) +
            " | EnsureBudgetKind=" + (phaseStats != null ? phaseStats.EnsureBudgetKind : string.Empty) +
            " | EnsureBudgetEnterDecision=" + (phaseStats != null ? phaseStats.EnsureBudgetEnterDecision : string.Empty) +
            " | EnsureBudgetEnterResetMs=" + (phaseStats != null ? phaseStats.EnsureBudgetEnterResetMs.ToString("F2") : "0.00") +
            " | EnsureBudgetEnterPlanetLaunchPopulationMs=" + (phaseStats != null ? phaseStats.EnsureBudgetEnterPlanetLaunchPopulationMs.ToString("F2") : "0.00") +
            " | EnsureBudgetEnterTotalBudgetMs=" + (phaseStats != null ? phaseStats.EnsureBudgetEnterTotalBudgetMs.ToString("F2") : "0.00") +
            " | EnsureBudgetEnterRefreshBudgetMs=" + (phaseStats != null ? phaseStats.EnsureBudgetEnterRefreshBudgetMs.ToString("F2") : "0.00")
            );
    }

    private void PrimeCurrentSystemShipsInSpaceBudgetCache(
    string systemId,
    IReadOnlyList<SystemNpcRuntimeState> npcs,
    int currentTick)
    {
        _currentSystemShipsInSpaceBudgetTick = currentTick;
        _currentSystemShipsInSpaceBudgetSystemId = systemId ?? string.Empty;
        _currentSystemShipsInSpaceBudgetCount = 0;

        if (string.IsNullOrWhiteSpace(systemId) ||
            npcs == null ||
            npcs.Count == 0)
        {
            return;
        }

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc == null)
                continue;

            if (!npc.IsAlive)
                continue;

            if (npc.LifeState != SystemNpcLifeState.Alive)
                continue;

            if (npc.IsOnPlanet)
                continue;

            _currentSystemShipsInSpaceBudgetCount++;
        }
    }
}