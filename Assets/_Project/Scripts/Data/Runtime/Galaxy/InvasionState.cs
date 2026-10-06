using System;
using System.Collections.Generic;

[Serializable]
public sealed class InvasionState
{
    public string InvasionId;
    public string FactionId;

    public string SourceSystemId;
    public string TargetSystemId;

    public int Level = 1;
    public int EscalationTier = 1;
    public int EscalationPressure = 1;
    public string EscalationRuleId = string.Empty;
    public bool UsesPlayerPowerScaling;

    public InvasionLifecycleState LifecycleState =
        InvasionLifecycleState.None;

    public List<string> EnemyGroupRuntimeIds = new();

    public int StartedAtTick;
    public int LastUpdatedTick;
    public int NextUpdateTick;
    public int ResolveAtTick;
    public int CleanupAfterTick;

    public bool IsActive()
    {
        return LifecycleState == InvasionLifecycleState.Preparing ||
               LifecycleState == InvasionLifecycleState.Active;
    }

    public void ApplyEscalation(
        string factionId,
        int galaxyLevel)
    {
        FactionId =
            NormalizeFactionId(factionId);

        Level =
            ClampGalaxyLevel(galaxyLevel);

        EscalationTier =
            CalculateEscalationTier(
                FactionId,
                Level);

        EscalationPressure =
            CalculateEscalationPressure(
                FactionId,
                Level,
                EscalationTier);

        EscalationRuleId =
            BuildEscalationRuleId(
                FactionId,
                Level,
                EscalationTier);

        UsesPlayerPowerScaling = false;
    }

    public static int CalculateEscalationTier(
        string factionId,
        int galaxyLevel)
    {
        int level =
            ClampGalaxyLevel(galaxyLevel);

        string normalizedFactionId =
            NormalizeFactionId(factionId);

        switch (normalizedFactionId)
        {
            case "infected":
                return ClampEscalationTier(1 + ((level - 1) / 2));

            case "ancients":
                return ClampEscalationTier(1 + ((level - 1) / 4));

            case "ai":
                return ClampEscalationTier(1 + ((level - 1) / 3));

            default:
                return ClampEscalationTier(1 + ((level - 1) / 3));
        }
    }

    public static int CalculateEscalationPressure(
        string factionId,
        int galaxyLevel,
        int escalationTier)
    {
        int level =
            ClampGalaxyLevel(galaxyLevel);

        int tier =
            ClampEscalationTier(escalationTier);

        string normalizedFactionId =
            NormalizeFactionId(factionId);

        int factionPressureBonus =
            normalizedFactionId == "infected"
                ? 2
                : normalizedFactionId == "ancients"
                    ? 1
                    : 0;

        return Math.Max(
            1,
            level + tier + factionPressureBonus);
    }

    private static string BuildEscalationRuleId(
        string factionId,
        int galaxyLevel,
        int escalationTier)
    {
        string normalizedFactionId =
            NormalizeFactionId(factionId);

        if (string.IsNullOrWhiteSpace(normalizedFactionId))
            normalizedFactionId = "unknown";

        return normalizedFactionId +
               "_gl" +
               ClampGalaxyLevel(galaxyLevel).ToString("00") +
               "_tier" +
               ClampEscalationTier(escalationTier).ToString("00");
    }

    private static int ClampGalaxyLevel(
        int galaxyLevel)
    {
        if (galaxyLevel < 1)
            return 1;

        if (galaxyLevel > 10)
            return 10;

        return galaxyLevel;
    }

    private static int ClampEscalationTier(
        int escalationTier)
    {
        if (escalationTier < 1)
            return 1;

        if (escalationTier > 5)
            return 5;

        return escalationTier;
    }

    private static string NormalizeFactionId(
        string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId)
            ? string.Empty
            : factionId.Trim().ToLowerInvariant();
    }
}