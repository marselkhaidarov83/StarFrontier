using System;
using System.Collections.Generic;
using System.Linq;

public static class ResearchRules
{
    public static IEnumerable<string> StateErrors(ResearchRuntimeState state)
    {
        if (state == null) yield break;
        var ids = new HashSet<ResearchUpgrade>();
        foreach (var item in state.Levels ?? new List<ResearchUpgradeLevel>())
        {
            if (item == null || item.Upgrade == ResearchUpgrade.None || !Enum.IsDefined(typeof(ResearchUpgrade), item.Upgrade) ||
                item.Level < 0 || item.Level > 10) { yield return "InvalidResearchLevel"; continue; }
            if (!ids.Add(item.Upgrade)) yield return "DuplicateResearchLevel";
        }
        var op = state.Active;
        if (op != null && !string.IsNullOrEmpty(op.OperationId))
        {
            if (state.ActiveUpgrade == ResearchUpgrade.None || !Enum.IsDefined(typeof(ResearchUpgrade), state.ActiveUpgrade) ||
                op.TargetLevel != state.GetLevel(state.ActiveUpgrade) + 1 || op.TargetLevel > 10 ||
                double.IsNaN(op.DurationSeconds) || double.IsInfinity(op.DurationSeconds) || op.DurationSeconds < 0 ||
                double.IsNaN(op.ElapsedSeconds) || double.IsInfinity(op.ElapsedSeconds) || op.ElapsedSeconds < 0 || op.ElapsedSeconds > op.DurationSeconds)
                yield return "InvalidResearchOperation";
        }
        else if (op != null && (op.Kind != StationOperationKind.None || op.DurationSeconds != 0 || op.ElapsedSeconds != 0 ||
            (op.PaidCosts != null && op.PaidCosts.Length > 0))) yield return "InvalidResearchIdentity";
    }

    public static IEnumerable<string> Errors(ResearchTreeConfig tree)
    {
        if (tree == null) { yield return "ResearchTreeMissing"; yield break; }
        var seen = new HashSet<ResearchUpgrade>();
        foreach (var branch in tree.Branches ?? Array.Empty<ResearchBranchConfig>())
        {
            if (branch == null) { yield return "NullBranch"; continue; }
            var nodes = branch.Nodes ?? Array.Empty<ResearchNode>();
            foreach (var node in nodes)
            {
                if (node == null || node.Upgrade == ResearchUpgrade.None || !Enum.IsDefined(typeof(ResearchUpgrade), node.Upgrade))
                { yield return "InvalidUpgrade"; continue; }
                if (!seen.Add(node.Upgrade)) yield return "DuplicateUpgrade";
                if (node.RequiredGalaxyLevel < 1 || node.RequiredGalaxyLevel > 10 ||
                    (node.Levels ?? Array.Empty<ResearchNodeLevel>()).Any(x => x != null && (x.RequiredGalaxyLevel < 1 || x.RequiredGalaxyLevel > 10)))
                    yield return "InvalidResearchGalaxyLevel";
                if (node.Levels == null || node.Levels.Length != 10 ||
                    Enumerable.Range(1, 10).Any(level => node.Levels.Count(x => x != null && x.Level == level) != 1))
                    yield return "TenLevelsRequired";
                foreach (var level in node.Levels ?? Array.Empty<ResearchNodeLevel>())
                {
                    if (level == null) continue;
                    foreach (var dep in DependenciesFor(node, level.Level))
                        if (dep == null || dep.RequiredLevel < 1 || dep.RequiredLevel > 10 ||
                            !nodes.Any(x => x != null && x.Upgrade == dep.Upgrade)) yield return "DependencyOutsideBranch";
                    if (Cycle(node, level.Level, nodes, new HashSet<string>(), new HashSet<string>()))
                        yield return "ResearchCycle";
                }
            }
        }
    }
    public static ResearchDependency[] DependenciesFor(ResearchNode node, int targetLevel) =>
        node?.Levels?.FirstOrDefault(x => x != null && x.Level == targetLevel)?.Dependencies ?? Array.Empty<ResearchDependency>();
    // Each level also depends on the preceding level of the same research.
    private static bool Cycle(ResearchNode node, int level, ResearchNode[] nodes, HashSet<string> path, HashSet<string> done)
    {
        if (level < 1 || level > 10) return false;
        string key = ((int)node.Upgrade) + ":" + level;
        if (path.Contains(key)) return true;
        if (done.Contains(key)) return false;
        path.Add(key);
        if (level > 1 && Cycle(node, level - 1, nodes, path, done)) return true;
        foreach (var dep in DependenciesFor(node, level))
        {
            var next = dep == null ? null : nodes.FirstOrDefault(x => x != null && x.Upgrade == dep.Upgrade);
            if (next != null && Cycle(next, dep.RequiredLevel, nodes, path, done)) return true;
        }
        path.Remove(key); done.Add(key); return false;
    }
    public static ResearchNode Find(ResearchTreeConfig tree, ResearchUpgrade upgrade) =>
        tree?.Branches?.Where(x => x != null).SelectMany(x => x.Nodes ?? Array.Empty<ResearchNode>()).FirstOrDefault(x => x != null && x.Upgrade == upgrade);
    public static ResearchBranchConfig BranchForStation(ResearchTreeConfig tree, StationType stationType) =>
        tree?.Branches?.FirstOrDefault(b => b != null && b.StationType == stationType);
    public static bool CanAccessUpgrade(ResearchTreeConfig tree, StationType stationType, ResearchUpgrade upgrade) =>
        stationType == StationType.Science || (BranchForStation(tree, stationType)?.Nodes ?? Array.Empty<ResearchNode>())
            .Any(n => n != null && n.Upgrade == upgrade);
    public static int RequiredGalaxyLevel(ResearchNode node, int targetLevel)
    {
        if (node == null) return 10;
        var level = node.Levels?.FirstOrDefault(x => x != null && x.Level == targetLevel);
        return Math.Max(1, Math.Max(node.RequiredGalaxyLevel, level?.RequiredGalaxyLevel ?? 1));
    }
    // Visibility follows level-specific prerequisites and their galaxy gates, even for saved completed levels.
    public static IEnumerable<ResearchNode> VisibleNodes(ResearchBranchConfig branch, int galaxyLevel)
    {
        foreach (var node in branch?.Nodes ?? Array.Empty<ResearchNode>())
            if (node != null && IsLevelVisible(branch, node, 1, galaxyLevel)) yield return node;
    }
    public static bool IsLevelVisible(ResearchBranchConfig branch, ResearchNode node, int targetLevel, int galaxyLevel) =>
        node != null && VisibleLevel(node, targetLevel, branch?.Nodes ?? Array.Empty<ResearchNode>(), galaxyLevel,
            new Dictionary<string, bool>(), new HashSet<string>());
    private static bool VisibleLevel(ResearchNode node, int level, ResearchNode[] nodes, int galaxyLevel,
        Dictionary<string, bool> cache, HashSet<string> path)
    {
        if (level < 1 || level > 10 || node.Levels?.Any(x => x != null && x.Level == level) != true) return false;
        string key = ((int)node.Upgrade) + ":" + level;
        if (cache.TryGetValue(key, out bool result)) return result;
        if (!path.Add(key)) return false;
        result = galaxyLevel >= RequiredGalaxyLevel(node, level);
        if (level > 1 && !VisibleLevel(node, level - 1, nodes, galaxyLevel, cache, path)) result = false;
        foreach (var dep in DependenciesFor(node, level))
        {
            var parent = dep == null ? null : nodes.FirstOrDefault(x => x != null && x.Upgrade == dep.Upgrade);
            if (parent == null || !VisibleLevel(parent, dep.RequiredLevel, nodes, galaxyLevel, cache, path)) result = false;
        }
        path.Remove(key); cache[key] = result; return result;
    }
    public static bool TryQuote(ResearchTreeConfig tree, ResearchRuntimeState state, ResearchUpgrade upgrade,
        string optionId, out StationOperationRuntimeState quote, out string reason, int galaxyLevel = 10)
    {
        quote = null; reason = "ResearchUnavailable";
        if (state == null || Errors(tree).Any() || StateErrors(state).Any()) return false;
        state.Normalize();
        if (state.Active != null) { reason = "ResearchAlreadyRunning"; return false; }
        var node = Find(tree, upgrade);
        int level = state.GetLevel(upgrade);
        if (node == null || level < 0 || level >= 10) return false;
        var branch = tree.Branches.FirstOrDefault(b => b != null && (b.Nodes ?? Array.Empty<ResearchNode>()).Contains(node));
        if (!IsLevelVisible(branch, node, level + 1, galaxyLevel))
        { reason = "GalaxyLevelRequired"; return false; }
        if (DependenciesFor(node, level + 1).Any(x => x == null || state.GetLevel(x.Upgrade) < x.RequiredLevel))
        { reason = "DependenciesNotMet"; return false; }
        var options = node.Levels.Single(x => x.Level == level + 1).PaymentOptions;
        var choices = options?.Where(x => x != null && x.Id == optionId).ToArray();
        if (choices == null || choices.Length != 1 || !choices[0].TryCreateSnapshot(StationOperationKind.Upgrade, level + 1, out quote))
        { reason = "InvalidPaymentOption"; return false; }
        reason = string.Empty; return true;
    }
    public static bool Advance(ResearchRuntimeState state, double seconds, double rate, out ResearchUpgrade upgrade, out int level)
    {
        upgrade = ResearchUpgrade.None; level = 0;
        if (state == null || seconds < 0 || rate <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) ||
            double.IsNaN(rate) || double.IsInfinity(rate)) return false;
        state.Normalize();
        var active = state.Active;
        if (StateErrors(state).Any()) return false;
        if (active == null || !Enum.IsDefined(typeof(ResearchUpgrade), state.ActiveUpgrade) || state.ActiveUpgrade == ResearchUpgrade.None ||
            active.TargetLevel != state.GetLevel(state.ActiveUpgrade) + 1 || active.TargetLevel > 10 ||
            double.IsNaN(active.DurationSeconds) || double.IsInfinity(active.DurationSeconds) || active.DurationSeconds < 0 ||
            double.IsNaN(active.ElapsedSeconds) || double.IsInfinity(active.ElapsedSeconds) || active.ElapsedSeconds < 0) return false;
        active.ElapsedSeconds = Math.Min(active.DurationSeconds, active.ElapsedSeconds + seconds * rate);
        state.Revision++;
        if (active.ElapsedSeconds < active.DurationSeconds) return false;
        upgrade = state.ActiveUpgrade; level = active.TargetLevel;
        var entry = state.Levels.Find(x => x.Upgrade == state.ActiveUpgrade);
        if (entry == null) { entry = new ResearchUpgradeLevel { Upgrade = state.ActiveUpgrade }; state.Levels.Add(entry); }
        entry.Level = level;
        state.Active = null; state.ActiveUpgrade = ResearchUpgrade.None;
        return true;
    }
}
