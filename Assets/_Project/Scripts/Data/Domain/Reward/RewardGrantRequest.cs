using System;
using System.Collections.Generic;

[Serializable]
public class RewardGrantRequest
{
    public string SourceId;
    public string SourceType;

    public int Credits;

    public List<RewardItemEntry> Items = new List<RewardItemEntry>();

    public static RewardGrantRequest CreditsOnly(string sourceId, string sourceType, int credits)
    {
        return new RewardGrantRequest
        {
            SourceId = sourceId,
            SourceType = sourceType,
            Credits = credits
        };
    }

    public static RewardGrantRequest ItemsOnly(string sourceId, string sourceType, List<RewardItemEntry> items)
    {
        return new RewardGrantRequest
        {
            SourceId = sourceId,
            SourceType = sourceType,
            Items = items ?? new List<RewardItemEntry>()
        };
    }
}