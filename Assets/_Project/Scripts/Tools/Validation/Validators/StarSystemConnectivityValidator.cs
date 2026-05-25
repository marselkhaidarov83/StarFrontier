using System.Collections.Generic;
using System.Linq;

public class StarSystemConnectivityValidator
{
    public List<ValidationIssue> Validate(
    IEnumerable<StarSystemConfig> systems,
    string startSystemId,
    bool requireBidirectionalLinks = true)
    {
        var issues = new List<ValidationIssue>();
        var systemList = systems.Where(s => s != null).ToList();
        var systemById = systemList.ToDictionary(s => s.Id, s => s);

        // 1. Проверка стартовой системы
        if (string.IsNullOrWhiteSpace(startSystemId))
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            "Start system id is empty.",
            null));
            return issues;
        }

        if (!systemById.TryGetValue(startSystemId, out var startSystem))
        {
            issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            $"Start system not found: {startSystemId}",
            null));
            return issues;
        }

        // 2. Проверка соседей на существование и пустые строки
        foreach (var system in systemList)
        {
            // if (system.NeighborSystemIds == null || system.NeighborSystemIds.Length == 0)
            if (system.LinkedSystems == null || system.LinkedSystems.Length == 0)
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "System has no neighbors.",
                system));
                continue;
            }

            // foreach (var neighborId in system.NeighborSystemIds)
            foreach (var linkedSystem in system.LinkedSystems)
            {
                // if (string.IsNullOrWhiteSpace(neighborId))
                if (string.IsNullOrWhiteSpace(linkedSystem.LinkedSystem.Id))
                {
                    issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "NeighborSystemIds contains an empty value.",
                    system));
                    continue;
                }

                // if (!systemById.ContainsKey(neighborId))
                if (!systemById.ContainsKey(linkedSystem.LinkedSystem.Id))
                {
                    issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    // $"Neighbor system does not exist: {neighborId}",
                    $"Neighbor system does not exist: {linkedSystem.LinkedSystem.Id}",
                    system));
                }
            }
        }

        // 3. Проверка двусторонности переходов
        if (requireBidirectionalLinks)
        {
            foreach (var system in systemList)
            {
                // if (system.NeighborSystemIds == null)
                if (system.LinkedSystems == null)
                    continue;

                // foreach (var neighborId in system.NeighborSystemIds)
                foreach (var linkedSystem in system.LinkedSystems)
                {
                    // if (string.IsNullOrWhiteSpace(neighborId))
                    if (string.IsNullOrWhiteSpace(linkedSystem.LinkedSystem.Id))
                        continue;

                    // if (!systemById.TryGetValue(neighborId, out var neighborSystem))
                    if (!systemById.TryGetValue(linkedSystem.LinkedSystem.Id, out var neighborSystem))
                        continue;

                    List<string> list = new List<string>();
                    if (neighborSystem.LinkedSystems != null)
                        foreach (StarSystemLink link in neighborSystem.LinkedSystems)
                            list.Add(link.LinkedSystem.Id);

                    // var backLinks = neighborSystem.NeighborSystemIds ?? new string[0];
                    // if (!backLinks.Contains(system.Id))
                    if (!list.Contains(system.Id))
                    {
                        issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        $"Missing back-link: {neighborSystem.Id} does not link back to {system.Id}",
                        system));
                    }
                }
            }
        }

        // 4. Проверка достижимости всех систем от стартовой
        var visited = new HashSet<string>();
        var queue = new Queue<string>();

        visited.Add(startSystem.Id);
        queue.Enqueue(startSystem.Id);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var currentSystem = systemById[currentId];

            // var neighbors = currentSystem.NeighborSystemIds ?? new string[0];
            // foreach (var neighborId in neighbors)
            foreach (StarSystemLink link in currentSystem.LinkedSystems)
            {
                // if (string.IsNullOrWhiteSpace(neighborId))
                if (string.IsNullOrWhiteSpace(link.LinkedSystem.Id))
                    continue;

                // if (!systemById.ContainsKey(neighborId))
                if (!systemById.ContainsKey(link.LinkedSystem.Id))
                    continue;

                // if (visited.Add(neighborId))
                if (visited.Add(link.LinkedSystem.Id))
                {
                    // queue.Enqueue(neighborId);
                    queue.Enqueue(link.LinkedSystem.Id);
                }
            }
        }

        foreach (var system in systemList)
        {
            if (!visited.Contains(system.Id))
            {
                issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                $"System is unreachable from start system {startSystemId}.",
                system));
            }
        }

        return issues;
    }
}