using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class DebugController2A
{
    [ContextMenu("STAR FRONTIER/Galaxy/Уровень галактики +1")]
    private void DebugIncreaseGalaxyLevel()
    {
        if (!DeveloperModeGate.CanRunExplicitCommand(Application.isPlaying, Application.isEditor, DeveloperModeGate.IsEnabled))
        {
            AppLog.Warning(DebugLogChannel.DebugTools, typeof(DebugController2A),
                "[GalaxyDebug] Rejected | Reason=PlayModeOrDeveloperModeRequired");
            return;
        }
        var registry = ServiceRegistry;
        if (registry == null || !registry.TryGet<IGameSessionService>(out var session) ||
            !registry.TryGet<IConfigService>(out var config) || !registry.TryGet<SimpleEventBus>(out var events))
        {
            AppLog.Warning(DebugLogChannel.DebugTools, typeof(DebugController2A),
                "[GalaxyDebug] Rejected | Reason=ServicesUnavailable");
            return;
        }
        if (!GalaxyDebugCommands2A.TryIncreaseLevel(session, config.GetAllSectors(), events, out string reason))
            AppLog.Warning(DebugLogChannel.DebugTools, typeof(DebugController2A), "[GalaxyDebug] Rejected | Reason=" + reason);
    }
}

/// <summary>The real galaxy level is the number of unlocked runtime sectors, not an NPC level override.</summary>
public static class GalaxyDebugCommands2A
{
    public static bool TryIncreaseLevel(IGameSessionService session, IReadOnlyList<SectorConfig> configs,
        SimpleEventBus events, out string reason)
    {
        reason = "SessionUnavailable";
        var sectors = session?.State?.Galaxy?.Sectors;
        if (sectors == null || configs == null || events == null) return false;
        int before = sectors.Count(x => x != null && x.IsUnlocked);
        if (before <= 0) { reason = "UnlockedSectorMissing"; return false; }
        if (before >= 10) { reason = "MaximumGalaxyLevel"; return false; }
        var next = configs.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
            .OrderBy(x => x.Order).ThenBy(x => x.Id, System.StringComparer.Ordinal)
            .Select(x => sectors.FirstOrDefault(s => s != null && s.SectorId == x.Id && !s.IsUnlocked))
            .FirstOrDefault(x => x != null);
        if (next == null) { reason = "NoLockedSector"; return false; }
        next.IsUnlocked = true;
        events.PublishSafely(new SectorUnlockedEvent(next.SectorId),
            e => AppLog.Warning(DebugLogChannel.DebugTools, "[GalaxyDebug] Sector observer failed | " + e.Message));
        events.PublishSafely(new SaveNeedEvent("debug_galaxy_level_increased"),
            e => AppLog.Warning(DebugLogChannel.DebugTools, "[GalaxyDebug] Save observer failed | " + e.Message));
        AppLog.Info(DebugLogChannel.DebugTools, "[GalaxyDebug] LEVEL_INCREASED | Before=" + before +
            " | After=" + (before + 1) + " | Sector=" + next.SectorId);
        reason = string.Empty; return true;
    }
}
