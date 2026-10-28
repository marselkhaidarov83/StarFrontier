using System;
using System.Collections.Generic;

/// <summary>Compares what the player saw in saved missions with the restored mission state.</summary>
public static class MissionRestoreChangeDetector
{
    private static GameRuntimeState _loadedState;
    private static Dictionary<string, Entry> _beforeLoadValidation;

    public sealed class Entry
    {
        public string Title;
        public MissionStatus Status;
        public string NextStep;
    }

    public static Dictionary<string, Entry> Capture(RuntimeMissionSaveBlock block, IConfigService config)
    {
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        if (block == null) return result;
        Add(block.ActiveMissions, config, result);
        Add(block.ArchivedMissions, config, result);
        Add(block.CompletedMissions, config, result);
        return result;
    }

    public static void RecordBeforeLoadValidation(GameRuntimeState state, IConfigService config)
    {
        _loadedState = state;
        _beforeLoadValidation = Capture(state?.MissionBlock, config);
    }

    public static Dictionary<string, Entry> TakeBeforeRestore(GameRuntimeState state,
        IConfigService config)
    {
        Dictionary<string, Entry> saved = object.ReferenceEquals(_loadedState, state)
            ? _beforeLoadValidation : null;
        _loadedState = null;
        _beforeLoadValidation = null;
        return saved ?? Capture(state?.MissionBlock, config);
    }

    public static string DescribeFirstChange(
        Dictionary<string, Entry> before, Dictionary<string, Entry> after)
    {
        if (before == null || after == null) return string.Empty;
        var ids = new List<string>(before.Keys);
        foreach (string id in after.Keys)
            if (!before.ContainsKey(id)) ids.Add(id);
        ids.Sort(StringComparer.Ordinal);

        foreach (string id in ids)
        {
            bool hadBefore = before.TryGetValue(id, out Entry oldEntry);
            bool hasAfter = after.TryGetValue(id, out Entry newEntry);
            string title = !string.IsNullOrWhiteSpace(newEntry?.Title)
                ? newEntry.Title : (!string.IsNullOrWhiteSpace(oldEntry?.Title) ? oldEntry.Title : "Миссия");
            if (hadBefore && !hasAfter)
                return "После загрузки миссия «" + title + "» больше недоступна.";
            if (!hadBefore && hasAfter)
                return "После загрузки появилась миссия «" + title + "». " + NextStepSuffix(newEntry);
            if (!hadBefore || !hasAfter) continue;
            if (oldEntry.Status != newEntry.Status)
                return "После загрузки миссия «" + title + "»: " + StatusText(newEntry.Status) + ". " +
                       NextStepSuffix(newEntry);
            if (MissionLifecycleRules.IsActive(newEntry.Status) &&
                !string.Equals(oldEntry.NextStep, newEntry.NextStep, StringComparison.Ordinal))
                return "После загрузки у миссии «" + title + "» изменился следующий шаг: " +
                       newEntry.NextStep;
        }
        return string.Empty;
    }

    private static void Add(List<MissionInstanceData> missions, IConfigService config,
        Dictionary<string, Entry> result)
    {
        if (missions == null) return;
        foreach (MissionInstanceData mission in missions)
        {
            if (mission == null || string.IsNullOrWhiteSpace(mission.MissionRuntimeId) ||
                result.ContainsKey(mission.MissionRuntimeId)) continue;
            result[mission.MissionRuntimeId] = new Entry
            {
                Title = mission.Title,
                Status = mission.Status,
                NextStep = MissionLifecycleRules.IsActive(mission.Status)
                    ? MissionUiText.NextStep(mission, config) : string.Empty
            };
        }
    }

    private static string NextStepSuffix(Entry entry)
    {
        return MissionLifecycleRules.IsActive(entry.Status) &&
               !string.IsNullOrWhiteSpace(entry.NextStep)
            ? "Следующий шаг: " + entry.NextStep : string.Empty;
    }

    private static string StatusText(MissionStatus status)
    {
        switch (status)
        {
            case MissionStatus.ReadyToComplete: return "награда готова";
            case MissionStatus.Completed: return "награда получена";
            case MissionStatus.Failed: return "провалена";
            case MissionStatus.Expired: return "срок истёк";
            case MissionStatus.Cancelled: return "отменена";
            case MissionStatus.Accepted:
            case MissionStatus.InProgress: return "выполняется";
            default: return "доступна";
        }
    }
}
