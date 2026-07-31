using System;
using System.Collections.Generic;

[Serializable]
public sealed class DiagnosticReport
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxRecentErrorCategories = 20;

    public int SchemaVersion = CurrentSchemaVersion;
    public string CreatedUtc;
    public string ApplicationVersion;
    public string BuildSha;
    public string BuildId;
    public string SceneName;
    public List<string> RecentErrorCategories = new();
    public DiagnosticStateSummary State = new();

    public static DiagnosticReport Create(
        string applicationVersion,
        string buildSha,
        string buildId,
        string sceneName,
        IEnumerable<string> recentErrorCategories,
        DiagnosticStateSummary state)
    {
        var report = new DiagnosticReport
        {
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            ApplicationVersion = SafeValue(applicationVersion),
            BuildSha = SafeValue(buildSha),
            BuildId = SafeValue(buildId),
            SceneName = SafeValue(sceneName),
            State = state ?? new DiagnosticStateSummary()
        };

        if (recentErrorCategories == null)
            return report;

        var uniqueCategories = new HashSet<string>(StringComparer.Ordinal);

        foreach (string category in recentErrorCategories)
        {
            if (report.RecentErrorCategories.Count >= MaxRecentErrorCategories)
                break;

            string normalizedCategory = SafeValue(category);

            if (normalizedCategory == DiagnosticSafeValues.Unavailable)
                continue;

            if (uniqueCategories.Add(normalizedCategory))
                report.RecentErrorCategories.Add(normalizedCategory);
        }

        return report;
    }

    private static string SafeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? DiagnosticSafeValues.Unavailable
            : value.Trim();
    }
}

[Serializable]
public sealed class DiagnosticStateSummary
{
    public string GameStateName = DiagnosticSafeValues.Unavailable;
    public string CurrentSystemId = DiagnosticSafeValues.Unavailable;
    public string CurrentPlanetId = DiagnosticSafeValues.Unavailable;
    public int SaveDataVersion;
    public int RegisteredServiceCount;
    public bool TickActive;
}

public static class DiagnosticSafeValues
{
    public const string Unavailable = "unavailable";
}
