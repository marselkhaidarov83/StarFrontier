using System;

/// <summary>Player-facing mission outcome copy, snapshotted when an offer is generated.</summary>
[Serializable]
public sealed class MissionOutcomeTexts
{
    public string ReadyToTurnIn;
    public string Completed;
    public string Failed;
    public string Expired;
    public string Cancelled;
    public string MissionCargoLost;
    public string SystemCaptured;
    public string BattleLostOrLeft;
    public string LeftBeforeVictory;
    public string BattleDefeated;
    public string NextInvasionStarted;
    public string RouteObjectiveUnavailable;

    public MissionOutcomeTexts Format(Func<string, string> replace) => new MissionOutcomeTexts
    {
        ReadyToTurnIn = replace(ReadyToTurnIn),
        Completed = replace(Completed),
        Failed = replace(Failed),
        Expired = replace(Expired),
        Cancelled = replace(Cancelled),
        MissionCargoLost = replace(MissionCargoLost),
        SystemCaptured = replace(SystemCaptured),
        BattleLostOrLeft = replace(BattleLostOrLeft),
        LeftBeforeVictory = replace(LeftBeforeVictory),
        BattleDefeated = replace(BattleDefeated),
        NextInvasionStarted = replace(NextInvasionStarted),
        RouteObjectiveUnavailable = replace(RouteObjectiveUnavailable)
    };

    public string For(MissionStatus status, string reason)
    {
        if (status == MissionStatus.ReadyToComplete) return ReadyToTurnIn;
        if (status == MissionStatus.Completed) return Completed;
        if (status == MissionStatus.Failed)
            return reason switch
            {
                "MissionCargoLost" => Pick(MissionCargoLost, Failed),
                "SystemCaptured" => Pick(SystemCaptured, Failed),
                "BattleLostOrLeft" => Pick(BattleLostOrLeft, Failed),
                "LeftBeforeVictory" => Pick(LeftBeforeVictory, Failed),
                "BattleDefeated" => Pick(BattleDefeated, Failed),
                _ => Failed
            };
        if (status == MissionStatus.Expired)
            return reason == "NextInvasionStarted" ? Pick(NextInvasionStarted, Expired) : Expired;
        if (status == MissionStatus.Cancelled)
            return reason == "RouteObjectiveUnavailable" ? Pick(RouteObjectiveUnavailable, Cancelled) : Cancelled;
        return string.Empty;
    }

    private static string Pick(string specific, string fallback) =>
        string.IsNullOrWhiteSpace(specific) ? fallback : specific;
}
