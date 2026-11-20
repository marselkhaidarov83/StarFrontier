using System.Collections.Generic;
using System.Linq;

public class StarSystemConnectivityValidator
{
    // Проверяем структуру ассетов, включая ещё не открытые маршруты.
    public List<ValidationIssue> Validate(IEnumerable<StarSystemConfig> systems,
        string startSystemId, bool requireBidirectionalLinks = true)
    {
        var issues = new List<ValidationIssue>();
        var byId = new Dictionary<string, StarSystemConfig>();
        foreach (StarSystemConfig system in systems ?? Enumerable.Empty<StarSystemConfig>())
        {
            if (system == null) continue;
            if (string.IsNullOrWhiteSpace(system.Id) || byId.ContainsKey(system.Id))
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "System ID is empty or duplicated.", system));
            else byId.Add(system.Id, system);
        }
        if (string.IsNullOrWhiteSpace(startSystemId) || !byId.ContainsKey(startSystemId))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Start system not found: {startSystemId}.", null));
            return issues;
        }
        foreach (StarSystemConfig system in byId.Values)
        {
            issues.AddRange(new StarSystemConfigValidator().Validate(system));
            if (system.Routes == null) continue;
            foreach (RouteConfig route in system.Routes)
            {
                StarSystemConfig other = route?.GetOtherSystem(system.Id);
                if (other == null || string.IsNullOrWhiteSpace(other.Id)) continue;
                if (!byId.ContainsKey(other.Id))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Route references missing system: {other.Id}.", system));
                else if (requireBidirectionalLinks && (other.Routes == null || !other.Routes.Contains(route)))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Route {route.Id} must be attached to both systems.", system));
            }
        }
        var visited = new HashSet<string> { startSystemId };
        var queue = new Queue<string>();
        queue.Enqueue(startSystemId);
        while (queue.Count > 0)
        {
            StarSystemConfig current = byId[queue.Dequeue()];
            if (current.Routes == null) continue;
            foreach (RouteConfig route in current.Routes)
            {
                StarSystemConfig other = route?.GetOtherSystem(current.Id);
                if (other != null && !string.IsNullOrWhiteSpace(other.Id) &&
                    byId.ContainsKey(other.Id) && visited.Add(other.Id)) queue.Enqueue(other.Id);
            }
        }
        foreach (StarSystemConfig system in byId.Values)
            if (!visited.Contains(system.Id))
                issues.Add(new ValidationIssue(ValidationSeverity.Error, $"System is unreachable from {startSystemId} via routes.", system));
        return issues;
    }
}
